# DCDSS Design Review

Reviewer: design-review subagent (fresh read, no authoring context).
Source: `.agents/tasks/design.md`.
Verified against: `DCDSS/DCDSS.csproj`, `DCDSS/Program.cs`, folder scaffold, and the original user request.

## Verdict

**CHANGES_REQUESTED** — 1 HIGH and 8 MEDIUM findings (plus 10 NITs). The design is strong and resolves the seven ambiguities the brief listed (confirmed below), but it has one feasibility gap that blocks two core modules, plus several correctness/security/POPIA issues that must be decided before implementation.

## Brief-ambiguity resolution check (confirmed)

Each ambiguity the brief left open is explicitly resolved in the design:

| Ambiguity | Resolution in design | Status |
|---|---|---|
| PK type | `int` auto-increment identity on every entity (Section 2) | Resolved ✓ |
| Admin bootstrap | Seed default admin on startup via `IAdminSeeder` (Section 8) | Resolved ✓ |
| CDS ruleset representation/extensibility | C# rule objects in `IClinicalRuleStore`; add rules by adding entries; new kinds = new model + evaluator branch (Section 6) | Resolved ✓ |
| Drug-allergy/interaction data source | Seed `ClinicalRuleStore` rules + patient allergies from `Patient.Allergies` (Sections 6, 5.5) | Resolved ✓ (but see M1 — storage form violates 3NF) |
| ReferenceRange parsing + edge cases | `IReferenceRangeParser` with range/value parsing and Indeterminate fallback (Section 7) | Resolved ✓ (but see M7 — threshold-form values misclassified) |
| Cascade delete policy | All FKs `Restrict`; no DELETE endpoints (Section 4) | Resolved ✓ |
| Appointment DateTime vs date-only | `DateTime` (date + time-of-day) (Section 3) | Resolved ✓ |

## Findings

### HIGH

**1. [HIGH] No endpoint to create `MedicalRecord` makes the Lab and Medication modules infeasible.**
Location: Section 5 (per-module endpoints) and Section 15 (file outline) — there is `IMedicalRecordRepository` but no `IMedicalRecordService`, no `MedicalRecordsController`, and no create endpoint.
Problem: `LabResult.RecordId` (FK→MedicalRecords) and `Medication.RecordId` (FK→MedicalRecords) are both required, so `POST /api/laboratory/order` and `POST /api/medications` can only target a `MedicalRecord` that already exists. Nothing in the design or the brief's endpoint list creates one (`POST /api/patients` creates a `Patient`, not a record; the admin seeder only seeds a user). Implemented as designed, a doctor can register a patient but can never start a visit, so lab ordering and prescribing can never be exercised. The design claims "no open questions," so this silent gap must be closed.
Fix: Add a visit-creation endpoint and its supporting types, e.g.:
- `POST /api/medical-records` — `[Authorize(Roles="Doctor,Nurse")]`; request `CreateMedicalRecordDto { int PatientId [Required], int DoctorId [Required], string Diagnosis [Required, MaxLength 500], string? TreatmentNotes [MaxLength 2000], DateTime VisitDate [Required] }`.
- `IMedicalRecordService.CreateAsync(...)` validates: PatientId exists (else 404), DoctorId exists and `Role==Doctor` (else 400), VisitDate not in future (else 400); creates `MedicalRecord` with `Status=Active`; returns `201` with `MedicalRecordDto`; audit `CREATE_MEDICAL_RECORD`.
- Add a row to the RBAC matrix (Section 5): `POST /api/medical-records` → Doctor ✅, Nurse ✅, others —.
- Add `MedicalRecordsController`, `IMedicalRecordService`, `MedicalRecordService` to the Section 15 file outline.
This unblocks `/api/laboratory/order` and `/api/medications`. (If the intent is instead that records are seeded/out-of-band, that must be stated explicitly with the creation mechanism — but a create endpoint is the only implementable path for a real clinical workflow.)

### MEDIUM

**2. [MEDIUM] `Patient.Allergies` as a comma-delimited string violates the explicit 3NF requirement.**
Location: Section 3 (Patient entity, `Allergies` column) vs. the brief's "normalized tables (at least 3NF)" requirement.
Problem: A comma-separated list of allergens in a single column is a repeating group in one field — a 1NF violation, hence not 3NF. The design calls it a "documented schema extension" but does not acknowledge that it breaks the stated normalization requirement, and the safety logic must string-split/trim to use it.
Fix: Normalize into a `PatientAllergies` table: `PatientAllergyId int PK (identity)`, `PatientId int FK→Patients OnDelete Restrict (indexed)`, `Allergen string Required MaxLength 100`, unique index on `(PatientId, Allergen)`. Remove `Patient.Allergies`. Add `IPatientAllergyRepository.GetByPatientAsync(patientId)`; `SafetyCheckService`/`CdsEngine` load allergens as rows (no comma parsing). If the delimited-string approach is deliberately retained, the design must explicitly state it is an accepted denormalization that waives the 3NF requirement and justify why — but given the brief's hard 3NF line, the normalized table is the correct fix.

**3. [MEDIUM] Audit `Details` would log patient PII, contradicting the design's own POPIA claim.**
Location: Section 9 (step 3: "Compose `Details` from route values (`id`) and whitelisted action arguments, excluding any argument whose name matches `Password|PasswordHash|Token|SigningKey`") vs. the same section's "POPIA: `Details` never contains … unnecessary PII; it holds identifiers and outcome flags only."
Problem: The exclusion list only drops `Password`/`PasswordHash`/`Token`/`SigningKey`. For `CREATE_PATIENT`, the action argument is `CreatePatientDto`, whose fields (`FirstName`, `Surname`, `Address`, `Phone`, `DateOfBirth`, `Allergies`) are PHI. Under the described rule those fields are serialized into `AuditLog.Details` — exactly the "unnecessary PII" the design says it never stores. For POST creates there is also no route `id` (the id is generated), so the "identifiers and outcome flags only" claim is not achievable with the described mechanism.
Fix: Make `Details` an allowlist, not a denylist. Define `Details` = `{ entityId, success }` only, where `entityId` is the route id for by-id actions and the created entity's id read from `context.Result` for POST creates; never serialize the request DTO. For login, `Details` = `{ username, success }`. Update Section 9 step 3 and the POPIA statement to match this literal rule.

**4. [MEDIUM] Default/placeholder secrets are shipped in `appsettings.json` with no enforcement.**
Location: Section 10 (JWT) and Section 11 (`appsettings.json` ships `JwtSettings.SigningKey = "REPLACE_WITH_A_LONG_RANDOM_SECRET_AT_LEAST_32_CHARACTERS_LONG"` and `AdminSeed.Password = "Admin@12345"`).
Problem: (a) The JWT signing key ships as a publicly-known placeholder. The design relies on an operator overriding it via env var; if they do not, HS256 signs with a forgeable key — the worst JWT failure mode (anyone can mint Administrator tokens). (b) A usable default admin password is committed to the repo. Both are mitigated only by README prose, with no fail-fast.
Fix: (a) In `Program.cs` (or an `IValidateOptions<JwtSettings>`), throw on startup if `SigningKey` is missing/empty, equals the placeholder, or is `< 32` bytes — fail fast rather than running forgeable. Replace the committed value with a clearly-invalid placeholder that the validator rejects. (b) Remove the committed `AdminSeed.Password` default; require `AdminSeed__Password` via environment variable/user-secrets, or have `AdminSeeder` generate a random password on first seed and log it once (Warning) for the operator to capture. README documents the env var.

**5. [MEDIUM] PHI read access is not audited, despite the brief's POPIA "every sensitive user action" requirement.**
Location: Section 9 ("read-only actions aren't audited") and the RBAC matrix (Section 5) — `GET /api/patients/{id}` and `GET /api/laboratory/patient/{patientId}` are read-only and therefore not audited.
Problem: Accessing a patient's details and lab history is processing of personal/health information; under POPIA, access (not just mutation) to personal information is a sensitive, auditable action. The design audits mutations only and does not document this as a scoped limitation, so PHI access is unaudited.
Fix: Pick one and state it. Recommended: add `[AuditAction("VIEW_PATIENT")]` to `GET /api/patients/{id}` and `[AuditAction("VIEW_LAB_HISTORY")]` to `GET /api/laboratory/patient/{patientId}` (the filter already supports reads — decoration is enough); `Details` = `{ id, success }` (per finding 3, no PII). Alternatively, explicitly document in Section 9 + README that audit covers mutating actions only and PHI read-access audit is a backlog item. Do not leave it implicit.

**6. [MEDIUM] Allergen matching semantics are ambiguous and the `ALG-002` seed rule is unresolved.**
Location: Section 6 — `AllergyConflict` ("fires when a patient's allergy list contains `Allergen`") and seed `ALG-002: Allergen "Sulfa"/"Sulfonamide"`.
Problem: (a) "contains `Allergen`" is ambiguous: is it exact case-insensitive token membership (consistent with the "case-insensitive exact match on `DrugName`" stated for `DrugInteractionPair`) or substring containment? The two rule kinds use different, unstated matching semantics. (b) `ALG-002` writes `"Sulfa"/"Sulfonamide"` as if one rule can hold two allergen names, but `AllergyConflict.Allergen` is a single `string`. The implementer cannot tell whether to create one rule (and with which allergen string) or two.
Fix: (a) State allergen matching explicitly — recommend exact, case-insensitive token match against the patient's allergy tokens (same rule as drug interactions). (b) Split `ALG-002` into `ALG-002a` (Allergen `"Sulfa"`) and `ALG-002b` (Allergen `"Sulfonamide"`), each with the same drug list. (If substring matching is chosen instead, document it, but splitting is cleaner and avoids conflating two distinct documented allergens.)

**7. [MEDIUM] Lab-result "recency" is inconsistent and undefined; stale results can drive drug-lab alerts.**
Location: Section 6 — `CdsEngine` "loads the patient's … lab results (via records)" (all) vs. `SafetyCheckService` "loads … recent lab results" (undefined "recent").
Problem: `CriticalLabRule` and `DrugLabConflictRule` evaluate per lab result. (a) The two services disagree on scope (all vs. "recent") and "recent" is not defined. (b) Iterating all historical results means an old abnormal creatinine can fire `DLC-001`/`DLC-002` even after the patient normalizes, producing stale false alerts. (c) The CdsEngine merge with explicit `LabResults` is "extend" (union) with no dedup, so a stored and a hypothetical result for the same test both evaluate.
Fix: Specify that lab-driven rules evaluate against the single most-recent result per `TestName` (`max(DateOrdered)`) for the patient, applied identically in `CdsEngine` and `SafetyCheckService`; replace "recent" with "latest result per test." For the CdsEngine merge, dedup by `TestName` (state whether explicit input overrides the stored latest, or is unioned with latest-per-test).

**8. [MEDIUM] `"<5"` / `">200"` result values are misclassified as High/Low (clinically wrong).**
Location: Section 7 (Result-value parsing + Classification).
Problem: The design parses `"<5"` to numeric `5` ("conservative") and `">200"` to `200`, then classifies against the range. For a result `"<5"` against a `"<5 mg/dL"` range (Max=5), the rule "value >= Max → High" flags a clearly-below-limit (normal) result as High — alert fatigue and a wrong `Status`. The exact numeric value is unknown for threshold-form results, so treating the bound as the value is incorrect.
Fix: When `ResultValue` is itself a threshold form (`"<X"` or `">X"`), classify `Status` as `Indeterminate` (value not precisely known); do not compare the parsed bound as if it were the value. Continue parsing plain numbers (`"70"`) and numeric ranges normally. Update Section 7 "Result-value parsing" and "Classification." (Note: `CriticalLabRule` numeric comparison is unaffected and remains correct, since a `"<2"` glucose still parses to `2` for the critical-low check — keep that path numeric.)

**9. [MEDIUM] Layering contradiction: generic `Repository<T>` and `UnitOfWork` are placed in `Services/` (Core) but Section 1 puts repositories in the Data layer.**
Location: Section 1 ("Data layer — `Data/` …, `Repositories/` …, `Entities/`"; "Data never references API/Core") vs. Section 15 (`$PROJ/Services/Repository.cs`, `$PROJ/Services/UnitOfWork.cs`) with concrete repos in `$PROJ/Repositories/`.
Problem: Concrete repositories in the Data layer derive from `Repository<T>`, which Section 15 places in `Services/` (Core). That makes Data depend on Core — directly contradicting Section 1's "Data never references API/Core" and creating a Core↔Data dependency tangle for repository code.
Fix: Move `$PROJ/Services/Repository.cs` and `$PROJ/Services/UnitOfWork.cs` to `$PROJ/Repositories/` (Data layer). Keep `IRepository<T>` and `IUnitOfWork` in `$PROJ/Services/Interfaces/` (Core), so Core depends on abstractions and Data implements them — matching Section 1. Update the Section 15 file paths.

### NIT

**10. [NIT] The audit filter's `LOGIN`→`LOGIN_FAILED` switching and its exception-safe execution are unspecified.**
Location: Section 5.1 ("Audit action `LOGIN` (success) / `LOGIN_FAILED` (failure)") and Section 9 (filter steps).
Problem: `AuditActionAttribute(string actionName)` carries a single static name (`"LOGIN"`); the design lists both `LOGIN` and `LOGIN_FAILED` as logged actions but does not say how the filter decides which to emit or how it determines success. It also says "After execution (including exceptions), call `LogAsync`" without specifying `try/finally`, so an implementer could write the filter such that a thrown action skips the audit write.
Fix: Specify the filter uses `try { await next(); } finally { LogAsync(...); }`. Success = `context.Exception == null && context.Result is { StatusCode: >= 200 and < 300 }`. For an action marked `LOGIN`, emit `"LOGIN_FAILED"` when success is false, else `"LOGIN"`. (With finding 3's allowlist `Details`, the login record is `{ username, success }`.)

**11. [NIT] Referenced domain models `LabResultInput`, `SafetyCheckResult`, and `SafetyConflict` are used but not defined.**
Location: Section 6 (`CdsEvaluationInput.LabResults` is `IList<LabResultInput>`; `CheckPrescriptionAsync → SafetyCheckResult { bool Allowed, IList<SafetyConflict> Conflicts }`) and Section 15 (the `Models/Cds` file list omits `LabResultInput` and `SafetyCheckResult`, and `SafetyConflict` has no field list).
Fix: Define them and add to the file list, e.g. `LabResultInput { string TestName, string ResultValue, string ReferenceRange }`; `SafetyConflict { string RuleId, CdsFindingType Type, CdsSeverity Severity, string Message, string? RelatedDrug, string? RelatedAllergen }`; `SafetyCheckResult { bool Allowed, IList<SafetyConflict> Conflicts }`. Specify the `SafetyConflict`→`SafetyWarningDto` field mapping and the meaning of `Allowed` (recommend: `Allowed = !Conflicts.Any(c => c.Severity == Critical)`, since `MedicationService` is the actual gate).

**12. [NIT] Pagination bound enforcement is unspecified for patients/audit and inconsistent with reports.**
Location: Section 5.2 ("page ≥1, pageSize 1–100") and 5.7 (audit "same paging rules") vs. 5.7 reports ("Validates ranges (400 if out of bounds)").
Fix: State one rule for all paged endpoints: clamp in the service (`page = max(1, page); pageSize = clamp(pageSize, 1, 100)`) and return 400 only if non-clampable (e.g. non-integer). Apply identically to patients, audit, and reports for consistency.

**13. [NIT] "future" date validation is described as if it were a data annotation, but no such built-in attribute exists.**
Location: Section 5.6 (`CreateAppointmentDto` … "AppointmentDate [Required] (DateTime, future)") and 5.5 (`StartDate` … "must not be in future").
Fix: Clarify these are service-enforced (`BusinessRuleException` → 400), not data annotations, so the implementer does not look for a non-existent `[Future]`/`[NotInFuture]` attribute. (Or define a custom attribute and add it to the file list — pick one.)

**14. [NIT] Drug/allergen name normalization is unspecified; matching relies on exact free-text input.**
Location: Section 6 (`DrugInteractionPair` "case-insensitive exact match on `DrugName`") vs. `Medication.DrugName` being free text (`MaxLength 100`).
Fix: Specify normalization (trim + case-insensitive compare) and state that seed names are canonical; document in the README that prescribers must use the canonical drug names (future: a controlled drug catalog). This prevents missed interactions from naming variation (e.g. "warfarin sodium" vs "Warfarin").

**15. [NIT] `PasswordHash` column length 256 may be tight for `PasswordHasher<User>` output.**
Location: Section 3 (User entity, `PasswordHash MaxLength 256`).
Fix: Use `MaxLength 512` (or `nvarchar(max)`-equivalent) to stay safe across PBKDF2 parameter changes; the Identity PBKDF2 base64 string (salt+hash+version tag) can approach the bound.

**16. [NIT] `AddDbContext` + `AddDbContextFactory` are both registered; the factory already registers a scoped context.**
Location: Section "Program.cs wiring".
Fix: Drop `AddDbContext<DcdssContext>` and rely on `AddDbContextFactory<DcdssContext>` (which by default also registers a scoped `DcdssContext` resolved from the factory), keeping the factory for `AuditLogService`. Or keep both but document why (e.g. pooled factory) to avoid the appearance of a duplicate registration.

**17. [NIT] Role-claim mapping relies on the default JWT inbound claim map.**
Location: Section 10 (claims `sub`/`role`/`name`) — `[Authorize(Roles="...")]` works because `JwtSecurityTokenHandler` maps `"role"` → `ClaimTypes.Role` by default.
Fix: Make it explicit/robust: set `TokenValidationParameters.RoleClaimType = ClaimTypes.Role` (or `JwtClaimTypes.Role`) and emit the role claim accordingly, so a future change to the default inbound map does not silently break RBAC.

**18. [NIT] "Current month/year" for reports and appointment times use UTC, not SAST.**
Location: Section 3 (all dates UTC; time-zone conversion out of scope) + Section 5.7 (month/year defaults "current").
Fix: Acknowledge the month-boundary effect (a SAST month can split across two UTC months) and appointment-time display (08:00 UTC vs 10:00 SAST) in the README "Known Limitations," or store appointment times and report boundaries in SAST. At minimum, document it so implementers do not assume local time.

**19. [NIT] Authorization is per-action only; no global authenticated-by-default fallback.**
Location: Section 5 (every action has `[Authorize(Roles=...)]`; login is the only anonymous route).
Fix: Add a global fallback policy requiring an authenticated user (`builder.Services.AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());`) and keep `[AllowAnonymous]` on login. Role requirements stay per-action. This prevents an accidentally-unattributed action from being exposed anonymously.

## Verified Assumptions

- **Project SDK/target and packages** (`DCDSS.csproj`): `Microsoft.NET.Sdk.Web`, `net8.0`, with `Pomelo.EntityFrameworkCore.MySql 8.0.3`, `Microsoft.AspNetCore.Authentication.JwtBearer 8.0.33`, `Microsoft.EntityFrameworkCore.Design 8.0.33`, `Swashbuckle.AspNetCore 6.6.2`. Confirms the design's "Pomelo 8.x", "JwtBearer", "EF Core migration tooling (8.0.33)" claims, and that `dotnet-ef 8.0.33` aligns with the EF Design package.
- **Pomelo 8.x maps `DateOnly` → MySQL `DATE`** (supported since Pomelo 6.0). Confirms the design's DOB mapping choice.
- **EF Core 8 + Pomelo 8.x: `int` keys get `ValueGeneratedOnAdd` by convention → MySQL `AUTO_INCREMENT`.** Confirms Section 2's "no explicit `UseMySqlIdentityColumn()` needed; explicit `ValueGeneratedOnAdd()` for clarity."
- **`Program.cs`** is the default minimal scaffold (`AddControllers`/Swagger/HttpsRedirection/Authorization/MapControllers). The design's full `Program.cs` wiring (Section: Program.cs wiring) replaces it cleanly with no pre-existing conflict.
- **Folder scaffold** (`Controllers`, `Data`, `DTOs`, `Entities`, `Middleware`, `Models`, `Repositories`+`Interfaces`, `Security`, `Services`+`Interfaces`, `Configuration`) matches the design's stated layering; the design's added folders (`Exceptions`, `Data/Configurations`, `Models/Cds`, `DTOs/<Module>`) are net-new and acceptable.
- **JWT design** (HS256, ≥32-byte key, `ValidateIssuer/Audience/Lifetime/IssuerSigningKey`, `ClockSkew=1m`, `ProblemDetails` with no stack traces in production) is sound and standard.
- **Password hashing** via `PasswordHasher<User>` (PBKDF2) is the correct, non-rolling-your-own choice; `PasswordHasher<TUser>` works with the plain `User` entity (no `IdentityUser` required) and is available via the `Microsoft.AspNetCore.App` shared framework (no extra package).
- **Cascade policy** (all `Restrict`, no DELETE endpoints) is internally consistent and appropriate for clinical data.
- **All seven brief-listed ambiguities are explicitly resolved** (see the table above).

## Unverified / Wrong Assumptions

- **Wrong:** "`Patient.Allergies` as a delimited string is a (normalized) schema extension." It is a repeating group in one column — a 1NF violation, hence not 3NF as the brief requires (finding 2).
- **Wrong:** "ResultValue `'<5'` compared as numeric `5` is conservative/correct." Against a `'<5 mg/dL'` normal range it flags a clearly-normal sub-limit result as `High` (finding 8).
- **Wrong:** "Audit `Details` holds identifiers and outcome flags only / never unnecessary PII." Under the described allowlist-from-action-arguments rule, create-patient audit entries would contain name/address/phone/allergies (finding 3).
- **Wrong:** "Data layer never references Core." Section 15 places `Repository<T>`/`UnitOfWork` in `Services/` (Core) while concrete repos (Data) derive from them, so Data references Core (finding 9).
- **Weak/unverified:** "README/env var instructs overriding the JWT signing key and admin password." Reliance on operator discipline with no fail-fast leaves forgeable tokens and a committed default admin credential (finding 4).
- **Unverified:** The design's claims about Pomelo/EF Core behavior are pinned to the version already in `DCDSS.csproj` (8.0.3 / 8.0.33), so they are verified — not assumptions.
