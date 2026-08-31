# TimePay — Security Model & Deployment Hardening Guide

## 1. Security Architecture & Principles

TimePay is designed around the principle of **Defense in Depth**:

```
                  ┌─────────────────────────────────────┐
                  │          TIMEPAY SECURITY           │
                  ├─────────────────────────────────────┤
                  │ 1. TimePay WPF UI (Lock/Countdown)  │
                  │ 2. TimePay Windows Background Svc   │
                  │ 3. Windows Standard User Accounts   │
                  │ 4. NTFS File & Database ACLs        │
                  │ 5. Windows Group Policy & Kiosk     │
                  └─────────────────────────────────────┘
```

> [!IMPORTANT]
> A desktop application running on Windows can only enforce complete lockout if the customer account does **NOT** possess Windows Administrator privileges.
> Always deploy TimePay with standard Windows user accounts for guests/customers.

---

## 2. Core Security Features

### A. Authentication & Cryptography
- **Password Hashing**: PBKDF2 (HMAC-SHA512) with 100,000 iterations, 512-bit derived key, and 256-bit cryptographically random salt (`PasswordHasher.cs`).
- **Constant-Time Verification**: Uses `CryptographicOperations.FixedTimeEquals` to prevent timing attacks.
- **Zero Plaintext Storage**: Passwords are never logged, persisted in plaintext, or transmitted unhashed.
- **Admin Session Timeout**: Automatically logs out administrative sessions after 15 minutes of inactivity.

### B. Expiration-Timestamp Timer Engine
- **Timestamp Truth**: Remaining time is derived from `ExpirationAt - UtcNow` rather than an in-memory decrementing counter.
- **Crash & Restart Survival**: Power cycles, OS reboots, or UI crashes do not reset or inflate usage balances.
- **Immutable Transaction Versioning**: Changing pricing rates never retroactively affects previously purchased sessions or historical transaction calculations.

### C. System Clock Tampering Protection
- **Backward Jump Detection**: Detects any backward system clock adjustments exceeding 5 seconds (`ClockTamperDetected`).
- **Defensive Expiration Adjustment**: Automatically shifts `ExpirationAt` backward to prevent users from gaining free time by modifying the Windows clock.
- **Audit Logging**: Logs suspicious clock jump attempts into SQLite with timestamps and details.

### D. Comprehensive Audit Trail
- Logs 15 auditable events (`AuditAction`):
  - Admin login successes and failed attempts
  - Time purchases and rate changes
  - Session start, pause, resume, and expiration
  - Manual PC locking and unlocking
  - Background service start and stop events
  - Clock change detection

---

## 3. Commercial Deployment & Hardening Guide

### Step 1: Windows Account Separation
1. Create a **Windows Administrator account** for the shop owner/operator:
   - Account Name: `TimePayAdmin` (or shop owner's name)
   - Secure, strong Windows password.
2. Create a **Standard Windows User account** for computer guests:
   - Account Name: `Guest` or `Customer`
   - Account Type: **Standard User** (Remove from Administrators group).

### Step 2: Restrict Database & Application Directory ACLs
Run the following PowerShell commands as Administrator to prevent standard users from modifying the SQLite database or application binaries:

```powershell
# Create database directory
$dbDir = "$env:ProgramData\TimePay"
New-Item -ItemType Directory -Path $dbDir -Force

# Lock down permissions: SYSTEM & Administrators have Full Control, Users have Read & Execute only
icacls $dbDir /inheritance:r
icacls $dbDir /grant:r "SYSTEM:(OI)(CI)F"
icacls $dbDir /grant:r "Administrators:(OI)(CI)F"
icacls $dbDir /grant:r "Users:(OI)(CI)RX"
```

### Step 3: Install & Configure TimePay Windows Service
To install `TimePayService` as an automatic background Windows Service with crash recovery:

```cmd
:: 1. Create the Windows Service
sc.exe create TimePayService binPath= "C:\Program Files\TimePay\TimePay.Service.exe" start= auto DisplayName= "TimePay Time Management Service"

:: 2. Configure Service Recovery (Restart automatically on crash)
sc.exe failure TimePayService reset= 86400 actions= restart/5000/restart/10000/restart/60000

:: 3. Start the service
sc.exe start TimePayService
```

### Step 4: Group Policy Hardening for Standard Users (Optional for Kiosk/Café)
For dedicated gaming cafés or public terminals, configure Group Policy (`gpedit.msc`) under `User Configuration -> Administrative Templates -> System -> Ctrl+Alt+Del Options`:

| Policy | Recommended Setting | Rationale |
|---|---|---|
| **Remove Task Manager** | Enabled (`DisableTaskMgr = 1`) | Prevents guests from attempting to kill client processes |
| **Remove Lock Computer** | Enabled | Avoids confusion with Windows Lock vs TimePay Lock |
| **Prevent Access to Registry Editing Tools** | Enabled | Prevents registry tampering |
| **Prevent Access to Command Prompt / PowerShell** | Enabled | Restricts CLI access for standard users |

---

## 4. Threat Matrix & Mitigations

| Threat Vector | Mitigation Strategy |
|---|---|
| **User presses Alt+F4 on Lock Screen** | `MainWindow.xaml.cs` intercepts `Closing` event and cancels close request unless admin is authenticated. |
| **User restarts PC to get more time** | `TimePayService` and `TimerEngine` read stored `ExpirationAt` from SQLite on startup. If time elapsed, state transitions to `Expired`. |
| **User turns back system clock** | `TimerEngine.TickAsync()` detects backwards jumps, reduces `ExpirationAt` by the jump amount, and writes audit record. |
| **User attempts brute-force admin login** | Failed attempts are logged with counter, password field cleared, and rate-limited. |
| **User modifies application settings DB** | Windows NTFS ACLs restrict write access on `%ProgramData%\TimePay\` to Administrators and SYSTEM only. |
