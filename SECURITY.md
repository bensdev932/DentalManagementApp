# Security Policy

The **Dental Clinic Management System** handles critical dental records, financial ledgers, and Personally Identifiable Information (PII) / Protected Health Information (PHI). We take security, data privacy, and confidentiality seriously.

---

## Supported Versions

Only the latest active release and current `master` branch receive security patches and updates.

| Version / Branch | Supported          | Status                     |
| ---------------- | ------------------ | -------------------------- |
| `master`         | :white_check_mark: | Active Development (.NET 10)|
| `1.0.x`          | :white_check_mark: | Current Stable Release     |
| `< 1.0`          | :x:                | Deprecated                 |

---

## Reporting a Vulnerability

> [!CAUTION]
> **DO NOT file public GitHub Issues, discussions, or pull requests for security vulnerabilities.** Publicly disclosing a potential exploit puts real clinical databases and patient information at risk.

If you discover a vulnerability or potential security flaw, please report it through one of the following responsible disclosure channels:

1. **GitHub Private Vulnerability Reporting (Preferred)**:
   - Go to the **Security** tab of this repository on GitHub.
   - Click **Report a vulnerability** to open a private advisory draft.

2. **Email Disclosure**:
   - Send details directly to: [bensdev932@gmail.com](mailto:bensdev932@gmail.com)
   - Subject: `[SECURITY VULNERABILITY] DentalManagementApp - <Brief Summary>`

### What to Include in Your Report
To help us investigate and triage effectively, please include:
- A detailed description of the vulnerability and attack vector.
- Step-by-step reproduction instructions or a minimal Proof of Concept (PoC).
- Potential impact (e.g., unauthorized data access, privilege escalation, session hijacking).
- Any proposed mitigations or code fixes, if available.

### Response Timeline
- **Initial Acknowledgment**: Within **48 hours** of receiving the report.
- **Triage & Assessment**: Within **72 hours** with severity rating (CVSS) and reproduction status.
- **Remediation & Patch**: We will work with you to test and release a patch before any public advisory is announced.
- **Credit**: We will gladly acknowledge and credit your responsible disclosure in release notes (unless you prefer anonymity).

---

## Security Architecture & Built-in Protections

The application incorporates defense-in-depth principles across all layers:

### 1. Zero-Trust Initial Setup
- The owner account registration endpoint (`/api/v1/auth/initial-setup`) operates under a **one-time genesis lock**. Once the initial clinic owner account is created, the setup locks permanently to prevent hostile account takeover.

### 2. Authentication & Session Security
- **JWT + Refresh Token Architecture**: Short-lived access tokens with cryptographically secure refresh tokens stored hashed in the database.
- **Immediate Kill-Switch**: Administrative deactivation (`PUT /api/v1/users/{id}/status`) immediately invalidates all active JWT sessions and refresh tokens across all devices.
- **Revoke Sessions Engine**: `/api/v1/users/{id}/revoke-sessions` terminates all concurrent sessions on demand.

### 3. Secrets & Configuration Management
- No production secrets or live database passwords are committed to source control.
- Configuration follows ASP.NET Core zero-trust principles: placeholders (`YOUR_SUPABASE_PASSWORD`) in configuration files trigger **fail-fast production boot validation** in `Program.cs` if environment variables (`ConnectionStrings__DefaultConnection`, `JwtSettings__Key`) are not explicitly configured in host environments (e.g., Render).

### 4. Database & Transport Security
- **Encrypted in Transit**: Enforced TLS 1.3 / HTTPS for API traffic and `sslmode=Require;Trust Server Certificate=true;` for Supabase PostgreSQL connections.
- **SQL Injection Prevention**: 100% parameterized queries via Entity Framework Core LINQ expressions.
- **Automated Cold Backups**: Regular scheduled database dumps to secure storage via `.github/workflows/db-backup.yml`.

### 5. Idempotent Offline Sync
- The offline synchronization engine (`/api/v1/sync/push`) uses client-generated GUID idempotency keys to eliminate mutation replay attacks and duplicate billing postings.
- Soft-delete tombstones prevent data collisions and ensure auditable patient histories.

### 6. Regulatory Privacy Compliance
- Built to adhere to principles outlined in the **Philippine Data Privacy Act of 2012 (Republic Act No. 10173)** regarding clinical data confidentiality, access restrictions, and lawful processing.

---

## Security Best Practices for Clinic Operators

If you are hosting or deploying this application:

1. **Environment Variables**: Always supply database credentials and JWT keys via host environment variables; never edit raw configuration JSON files on production servers.
2. **Password Strength**: Enforce strong, high-entropy passwords (minimum 12 characters with mixed cases, numbers, and symbols) for clinic staff.
3. **Database Rotation**: Periodically rotate your Supabase database user passwords and Render connection strings.
4. **Access Control**: Reserve `Owner` administrative privileges strictly for licensed dentists or authorized clinic managers.

