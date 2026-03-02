#!/usr/bin/env python3
"""
Migrate Entra ID SFTP security groups into ThirdParty registry.

Queries Graph API for groups matching "SFTP - {Company} - {Role}",
derives unique companies, creates ThirdParty records in Table Storage
(if they don't already exist), and populates group IDs.

Run in Azure Cloud Shell. No external dependencies required.

Prerequisites:
  - az login (Cloud Shell handles this automatically)
  - Storage Table Data Contributor role on sftcredigyfilesdevapp
"""

import json
import re
import subprocess
import sys
import uuid
from difflib import SequenceMatcher

# ── Configuration ────────────────────────────────────────────────────────────
STORAGE_ACCOUNT = "sftcredigyfilesdevapp"
TABLE_NAME = "SftRegistry"
PARTITION_KEY = "ThirdParty"
CONTAINER_PREFIX = "sft-"
GRAPH_API = "https://graph.microsoft.com/v1.0"
MATCH_THRESHOLD = 0.55  # minimum fuzzy score for matching existing records


def az(args: list[str]) -> str:
    """Run an az CLI command and return stdout."""
    result = subprocess.run(
        ["az"] + args,
        capture_output=True, text=True, timeout=60
    )
    if result.returncode != 0:
        raise RuntimeError(f"az {' '.join(args[:3])}... failed:\n{result.stderr.strip()}")
    return result.stdout.strip()


def az_json(args: list[str]):
    """Run an az CLI command and return parsed JSON."""
    return json.loads(az(args))


# ── Step 1: Fetch SFTP groups from Entra ID ─────────────────────────────────

def fetch_sftp_groups() -> list[dict]:
    """Query Graph API for all groups starting with 'SFTP - '."""
    print("Fetching SFTP groups from Entra ID...")
    groups = []
    url = f"{GRAPH_API}/groups?$filter=startswith(displayName,'SFTP - ')&$select=id,displayName&$top=999"

    while url:
        data = az_json(["rest", "--method", "GET", "--url", url])
        groups.extend(data.get("value", []))
        url = data.get("@odata.nextLink")

    print(f"  Found {len(groups)} SFTP groups")
    return groups


def parse_group_name(display_name: str) -> tuple[str | None, str | None]:
    """
    Parse 'SFTP - Company Name - Read and Execute' or 'SFTP - Company Name - Modify'
    into (company_name, role).

    Mapping:
      Read and Execute → user_group  (UserGroupId — read/upload/download)
      Modify           → admin_group (AdminGroupId — full access + delete)
      Full Control     → ignored

    Returns (None, None) if the name doesn't match or is Full Control.
    """
    match = re.match(
        r"^\s*SFTP\s*-\s*(.+?)\s*-\s*(Read\s+and\s+Execute|Modify|Full\s*Control)\s*$",
        display_name,
        re.IGNORECASE,
    )
    if not match:
        return None, None

    company = match.group(1).strip()
    raw_role = match.group(2).strip().lower()

    if "full" in raw_role:
        return None, None  # skip Full Control groups

    if "read" in raw_role:
        return company, "user_group"
    else:  # modify
        return company, "admin_group"


def build_company_map(groups: list[dict]) -> dict[str, dict]:
    """
    Parse all groups and build a map of unique companies with their group IDs.

    Returns: { "Company Name": { "modify_id": ..., "modify_name": ...,
                                  "full_control_id": ..., "full_control_name": ... } }
    """
    companies: dict[str, dict] = {}
    skipped = []

    for g in groups:
        company, role = parse_group_name(g["displayName"])
        if company and role:
            if company not in companies:
                companies[company] = {}
            companies[company][f"{role}_id"] = g["id"]
            companies[company][f"{role}_name"] = g["displayName"]
        else:
            skipped.append(g["displayName"])

    if skipped:
        print(f"\n  Skipped {len(skipped)} groups (Full Control or unrecognized):")
        for name in skipped[:5]:
            print(f"    - {name}")
        if len(skipped) > 5:
            print(f"    ... and {len(skipped) - 5} more")

    print(f"  Derived {len(companies)} unique companies")
    return companies


# ── Step 2: Table Storage operations ─────────────────────────────────────────

def ensure_table_exists():
    """Create the SftRegistry table if it doesn't exist."""
    print(f"Ensuring table {TABLE_NAME} exists...")
    try:
        az([
            "storage", "table", "create",
            "--account-name", STORAGE_ACCOUNT,
            "--name", TABLE_NAME,
            "--auth-mode", "login",
        ])
        print("  Table created")
    except RuntimeError as e:
        if "TableAlreadyExists" in str(e):
            print("  Table already exists")
        else:
            raise


def fetch_third_parties() -> list[dict]:
    """Query the SftRegistry table for all ThirdParty entities."""
    print(f"Fetching existing ThirdParty records...")
    raw = az([
        "storage", "entity", "query",
        "--account-name", STORAGE_ACCOUNT,
        "--table-name", TABLE_NAME,
        "--filter", f"PartitionKey eq '{PARTITION_KEY}'",
        "--auth-mode", "login",
        "--output", "json",
    ])
    entities = json.loads(raw)
    if isinstance(entities, dict):
        entities = entities.get("items", [])
    print(f"  Found {len(entities)} existing records")
    return entities


# ── Step 3: Sanitization (matches API logic) ─────────────────────────────────

def sanitize_company_name(name: str) -> str:
    """
    Sanitize a company name for use as an Azure container name suffix.
    Must match OnboardingService.SanitizeCompanyName() exactly.
    """
    sanitized = re.sub(r"[^a-z0-9-]", "-", name.lower()).strip("-")
    # Collapse consecutive hyphens
    sanitized = re.sub(r"-+", "-", sanitized)
    return sanitized[:50]


def generate_id() -> str:
    """Generate a ThirdParty ID matching the API format: tp-{8 hex chars}."""
    return f"tp-{uuid.uuid4().hex[:8]}"


# ── Step 4: Fuzzy matching for existing records ──────────────────────────────

def normalize(name: str) -> str:
    """Normalize a name for comparison."""
    name = name.lower()
    name = re.sub(r"[^a-z0-9\s]", " ", name)
    name = re.sub(r"\s+", " ", name).strip()
    return name


def fuzzy_score(a: str, b: str) -> float:
    """Return a similarity score between 0 and 1."""
    return SequenceMatcher(None, normalize(a), normalize(b)).ratio()


# ── Step 5: Plan creation and updates ────────────────────────────────────────

def plan_actions(
    companies: dict[str, dict],
    existing: list[dict],
) -> tuple[list[dict], list[dict]]:
    """
    Determine what to create vs update.

    Returns (to_create, to_update) where each entry has the info needed to act.
    """
    to_create = []
    to_update = []

    # Index existing by company name (exact) and collect for fuzzy matching
    existing_by_name: dict[str, dict] = {}
    for party in existing:
        name = party.get("CompanyName", "")
        existing_by_name[name.lower()] = party

    for company_name, group_info in sorted(companies.items()):
        user_group_id = group_info.get("user_group_id")
        admin_group_id = group_info.get("admin_group_id")

        # Try exact match first
        party = existing_by_name.get(company_name.lower())

        # Try fuzzy match if no exact match
        if not party:
            best_score = 0.0
            best_party = None
            for ex in existing:
                score = fuzzy_score(company_name, ex.get("CompanyName", ""))
                if score > best_score:
                    best_score = score
                    best_party = ex
            if best_party and best_score >= MATCH_THRESHOLD:
                party = best_party

        if party:
            # Existing record — check if group IDs need updating
            current_user = party.get("UserGroupId", "")
            current_admin = party.get("AdminGroupId", "")
            if current_user == (user_group_id or "") and current_admin == (admin_group_id or ""):
                continue  # already up to date
            to_update.append({
                "party": party,
                "company_name": company_name,
                "user_group_id": user_group_id,
                "admin_group_id": admin_group_id,
                "user_group_name": group_info.get("user_group_name", ""),
                "admin_group_name": group_info.get("admin_group_name", ""),
            })
        else:
            # New record
            to_create.append({
                "company_name": company_name,
                "container_name": f"{CONTAINER_PREFIX}{sanitize_company_name(company_name)}",
                "user_group_id": user_group_id,
                "admin_group_id": admin_group_id,
                "user_group_name": group_info.get("user_group_name", ""),
                "admin_group_name": group_info.get("admin_group_name", ""),
            })

    return to_create, to_update


# ── Step 6: Display plan ─────────────────────────────────────────────────────

def display_plan(to_create: list[dict], to_update: list[dict]):
    """Show what will be created and updated."""
    if to_create:
        print(f"\n{'='*80}")
        print(f"  NEW RECORDS — {len(to_create)} third parties to create")
        print(f"{'='*80}")
        print(f"{'#':<4} {'Company':<30} {'Container':<30} {'R&E':<5} {'Modify':<5}")
        print("-" * 78)
        for i, c in enumerate(to_create, 1):
            has_re = "Y" if c["user_group_id"] else "-"
            has_mod = "Y" if c["admin_group_id"] else "-"
            print(f"{i:<4} {c['company_name'][:28]:<30} {c['container_name'][:28]:<30} {has_re:<5} {has_mod:<5}")
            if not c["user_group_id"]:
                print(f"     ^ WARNING: No Read and Execute group")
            if not c["admin_group_id"]:
                print(f"     ^ WARNING: No Modify group")

    if to_update:
        print(f"\n{'='*80}")
        print(f"  UPDATES — {len(to_update)} existing records to update")
        print(f"{'='*80}")
        print(f"{'#':<4} {'Company':<30} {'Read & Execute Group':<38} {'Modify Group':<38}")
        print("-" * 114)
        for i, u in enumerate(to_update, 1):
            party_name = u["party"].get("CompanyName", "?")[:28]
            ug = u["user_group_name"][:36] if u["user_group_name"] else "(none)"
            ag = u["admin_group_name"][:36] if u["admin_group_name"] else "(none)"
            print(f"{i:<4} {party_name:<30} {ug:<38} {ag:<38}")

    if not to_create and not to_update:
        print("\nEverything is up to date. Nothing to do.")


# ── Step 7: Apply changes ────────────────────────────────────────────────────

def create_party(entry: dict):
    """Insert a new ThirdParty entity into Table Storage."""
    tp_id = generate_id()
    entity_parts = [
        f"PartitionKey={PARTITION_KEY}",
        f"RowKey={tp_id}",
        f"CompanyName={entry['company_name']}",
        f"ContainerName={entry['container_name']}",
        "Status=active",
        "AutomationEnabled=false",
    ]
    if entry["user_group_id"]:
        entity_parts.append(f"UserGroupId={entry['user_group_id']}")
    if entry["admin_group_id"]:
        entity_parts.append(f"AdminGroupId={entry['admin_group_id']}")

    az([
        "storage", "entity", "insert",
        "--account-name", STORAGE_ACCOUNT,
        "--table-name", TABLE_NAME,
        "--entity", *entity_parts,
        "--auth-mode", "login",
    ])


def update_party(entry: dict):
    """Merge group IDs into an existing ThirdParty entity."""
    row_key = entry["party"]["RowKey"]
    entity_parts = [
        f"PartitionKey={PARTITION_KEY}",
        f"RowKey={row_key}",
    ]
    if entry["user_group_id"]:
        entity_parts.append(f"UserGroupId={entry['user_group_id']}")
    if entry["admin_group_id"]:
        entity_parts.append(f"AdminGroupId={entry['admin_group_id']}")

    az([
        "storage", "entity", "merge",
        "--account-name", STORAGE_ACCOUNT,
        "--table-name", TABLE_NAME,
        "--entity", *entity_parts,
        "--auth-mode", "login",
    ])


def apply_changes(to_create: list[dict], to_update: list[dict]):
    """Apply all confirmed changes to Table Storage."""
    total = len(to_create) + len(to_update)
    i = 0

    for entry in to_create:
        i += 1
        print(f"  [{i}/{total}] Creating {entry['company_name']}...", end=" ", flush=True)
        try:
            create_party(entry)
            print("OK")
        except Exception as e:
            print(f"FAILED: {e}")

    for entry in to_update:
        i += 1
        name = entry["party"].get("CompanyName", "?")
        print(f"  [{i}/{total}] Updating {name}...", end=" ", flush=True)
        try:
            update_party(entry)
            print("OK")
        except Exception as e:
            print(f"FAILED: {e}")


# ── Main ─────────────────────────────────────────────────────────────────────

def main():
    print("=" * 60)
    print("  SFTP Third-Party Migration")
    print("=" * 60)

    # 1. Fetch and parse groups
    groups = fetch_sftp_groups()
    if not groups:
        print("\nNo SFTP groups found. Check that groups exist with names like:")
        print('  "SFTP - Company Name - Modify"')
        print('  "SFTP - Company Name - Full Control"')
        sys.exit(1)

    companies = build_company_map(groups)
    if not companies:
        print("\nNo valid company groups parsed.")
        sys.exit(1)

    # 2. Ensure table exists and fetch existing records
    ensure_table_exists()
    existing = fetch_third_parties()

    # 3. Plan
    print("\nPlanning actions...")
    to_create, to_update = plan_actions(companies, existing)
    display_plan(to_create, to_update)

    if not to_create and not to_update:
        sys.exit(0)

    # 4. Confirm
    total = len(to_create) + len(to_update)
    parts = []
    if to_create:
        parts.append(f"create {len(to_create)}")
    if to_update:
        parts.append(f"update {len(to_update)}")
    print(f"\nReady to {' and '.join(parts)} records ({total} total).")
    response = input("Proceed? [y/N] ").strip().lower()
    if response not in ("y", "yes"):
        print("Aborted.")
        sys.exit(0)

    # 5. Apply
    print("\nApplying changes...")
    apply_changes(to_create, to_update)
    print(f"\nDone. Processed {total} records.")


if __name__ == "__main__":
    main()
