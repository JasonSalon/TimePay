# TimePay ⏱️💰

**Windows PC Time Management and Pay-to-Use System**

TimePay is a Windows desktop application and background service that manages computer access based on purchased usage time (e.g. **₱1 = 4 minutes**). Designed for internet cafés, computer rental shops, and controlled guest environments.

---

## 🌟 Key Features

- **Dynamic Pricing & Rate Calculator**: Configurable currency (PHP ₱, USD $, EUR €, JPY ¥, SGD S$, MYR RM) and conversion rates (`MinutesPerPeso`) with instant live preview.
- **Expiration-Timestamp Timer Engine**: Derive remaining time directly from `ExpirationAt` to eliminate timer drift and manipulation.
- **Persistent Sessions**: Purchased time persists across application crashes, Windows restarts, user logouts, and computer shutdowns.
- **System Clock Tampering Protection**: Detects backwards clock jumps, defends against free time acquisition, and records security audit logs.
- **Admin Dashboard**: Real-time session monitoring, 1-click timer pause/resume, manual lock, time additions, rate configuration, and settings.
- **Add Time UI**: Live amount-to-time conversion preview, preset quick-buttons (`+₱5`, `+₱10`, `+₱20`, `+₱50`, `+₱100`, `+₱200`), and transaction recording.
- **User Dashboard & Mini Widget**: Clean digital countdown widget, color-coded status transitions (Active/Low-Time/Critical/Paused), audible warning cues, and an always-on-top draggable floating pill.
- **Full Lock Screen**: Fullscreen blocking overlay when usage expires, preventing unauthorized desktop access until unlocked by an administrator.
- **Windows Background Service**: `TimePayService` background worker monitoring sessions, enforcing expiration, and communicating with the UI via Named Pipe IPC.
- **Transaction History & CSV Export**: Immutable financial audit records with rate versioning, date range filters, search, and CSV export for cashier handover.
- **System Audit Log Viewer**: Detailed tracking of logins, failed attempts, time purchases, rate changes, lock events, service lifecycle, and clock adjustments.
- **Security & PBKDF2 Hashing**: Passwords secured via PBKDF2 HMAC-SHA512 (100,000 iterations, 512-bit hash, 256-bit salt, constant-time comparison).

---

## 🏗️ Architecture

The solution is divided into 4 modular projects + automated test suite:

```
TimePay/
├── TimePay.sln
│
├── TimePay.Core/                ← Core models, interfaces, timer engine & calculation
│   ├── Models/                  ← AdminUser, Session, Settings, Transaction, AuditLog, AppState
│   ├── Interfaces/              ← ITimerEngine, ISessionManager, IAuthService, IStartupService, ITimeCalculator
│   ├── Security/                ← PasswordHasher (PBKDF2 SHA512)
│   ├── Timer/                   ← TimerEngine (Expiration tracking, clock tampering defense)
│   ├── TimeCalculation/         ← TimeCalculator (Amount × Rate = Minutes)
│   └── Ipc/                     ← Named Pipe IPC protocol and DTOs
│
├── TimePay.Data/                ← Data persistence layer (EF Core + SQLite)
│   ├── TimePayDbContext.cs      ← 5 tables with indexes, relations, and ISO-8601 converters
│   ├── DatabaseInitializer.cs   ← Automatic DB creation & default settings seed
│   └── Repositories/            ← SessionRepository, AuthRepository, SettingsRepository, TransactionRepository, AuditLogRepository
│
├── TimePay.App/                 ← WPF Desktop Application (.NET 8 Windows)
│   ├── Themes/DarkTheme.xaml    ← Dark dashboard design system tokens & controls
│   ├── Services/                ← NavigationService, AdminSessionService, IpcClient, WindowsStartupService
│   └── Views/
│       ├── SetupWizard.xaml     ← First-run initial configuration wizard
│       ├── AdminLogin.xaml      ← Administrator authentication
│       ├── AdminDashboard.xaml  ← Control panel with live timer and quick actions
│       ├── UserDashboard.xaml   ← Standard user timer view
│       ├── TimerWidgetWindow.xaml ← Floating always-on-top draggable widget pill
│       ├── LockScreen.xaml      ← Fullscreen lock overlay with admin unlock modal
│       ├── AddTime.xaml         ← Add time calculator & purchase confirmation
│       ├── TimeConfiguration.xaml ← Rate & currency settings with live preview
│       ├── Transactions.xaml    ← Financial records, filters & CSV export
│       ├── AuditLogsView.xaml   ← Security audit viewer & CSV export
│       └── SettingsView.xaml    ← Thresholds, sounds, auto-start, password update
│
├── TimePay.Service/             ← Windows Worker Service (.NET 8)
│   ├── Program.cs               ← WindowsService host & dependency injection
│   ├── TimerWorker.cs           ← Continuous 1-second timer monitor & lifecycle logging
│   └── Ipc/PipeServer.cs        ← Named Pipe IPC Server (\\.\pipe\TimePay_Service_Ipc_Pipe)
│
├── TimePay.Tests/               ← Automated Unit & Integration Tests (xUnit)
│   ├── DatabaseTests.cs         ← SQLite CRUD, seeding, time calculation, password hashing
│   ├── AuthenticationTests.cs   ← Login validation, failed attempt tracking, password change
│   ├── TimeConfigurationTests.cs ← Rate persistence, validation, immutability
│   ├── TimerEngineTests.cs      ← Ticks, expiration, warnings, pause/resume, clock tampering
│   ├── AddTimeTests.cs          ← Add time workflows, balance extension, rate versioning
│   ├── IpcAndServiceTests.cs    ← Named Pipe DTOs, service lifecycle logs
│   ├── SettingsAndStartupTests.cs ← Settings updates, threshold validation, password update
│   ├── TransactionsFilterTests.cs ← Revenue summation, date filtering, search
│   └── AuditLogViewerTests.cs   ← Audit log categories, limits, and searching
│
├── SECURITY.md                  ← Security model, threat matrix & Windows hardening guide
└── SPECIFICATION.md             ← Master project specification
```

---

## 🚀 Getting Started

### Prerequisites
- Windows 10 / Windows 11
- .NET 8.0 SDK (installed at `C:\Program Files\dotnet\`)

### Building the Solution
```powershell
$env:Path = "C:\Program Files\dotnet;" + $env:Path
dotnet build TimePay.sln
```

### Running Automated Tests
```powershell
$env:Path = "C:\Program Files\dotnet;" + $env:Path
dotnet test TimePay.Tests/TimePay.Tests.csproj --verbosity normal
```

### Running the Desktop Application
```powershell
$env:Path = "C:\Program Files\dotnet;" + $env:Path
dotnet run --project TimePay.App/TimePay.App.csproj
```

---

## 🔒 Security Model

For complete commercial hardening instructions, please refer to [SECURITY.md](SECURITY.md).