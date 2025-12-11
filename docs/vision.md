# Credigy Files – Vision

- Provide secure, self-service file exchange between internal finance users and external vendors.
- Use Azure Static Web Apps + Go Function App backend; data stored in ADLS Gen2 with hierarchical namespaces.
- Enforce identity via Entra ID (OIDC) with group-based RBAC; guests onboarded for vendors.
- Keep the data plane private (VNet + Private Endpoint), expose the web experience via files.credigy.com.
- Deliver a familiar Inbound/Outbound folder model, small PDF-centric files, low daily volume.
- Include admin controls for mappings, limits, and audit visibility.
