# TombForge / Warhammer40k.11 — Setup & Azure Checklist (M0 → M1)

This is the list of things **you** do outside the code: local tooling, GitHub, and the
Azure Portal. Items are tagged **[NOW]** (needed to run/deploy the M0 skeleton) or
**[M1]** (needed when we add Azure Table Storage persistence).

Architecture recap: **Blazor WASM (UI)** → same-origin **`/api`** (Azure Functions, server-side)
→ **Azure Table Storage** (M1). Login is handled by **Static Web Apps built-in auth** (GitHub).

---

## A. Local development tooling

| Tool | Status | Action |
|------|--------|--------|
| .NET SDK 10 | ✅ installed (10.0.301) | — |
| Node + npm | ✅ installed (24.x) | — |
| Azure Functions Core Tools | ✅ installed (func 4.9.0) | — |
| **SWA CLI** | ❌ not installed | **[NOW]** `npm i -g @azure/static-web-apps-cli` |
| Azurite (storage emulator) | ships with Visual Studio 2026 | **[M1]** start it before running the API locally (VS auto-starts it; or `npm i -g azurite` then run `azurite`) |
| Azure account | — | **[NOW for deploy]** free sign-up at https://azure.microsoft.com/free |

### Run the UI only (no API/login)
```powershell
dotnet run --project Warhammer40k.11
```
The whoami card shows **"Not signed in"** — expected, because `/api` and `/.auth` don't exist
under a plain `dotnet run`.

### Run the full stack with login emulation (UI + API + auth)
```powershell
# from the repo root
swa start warhammer40k
```
This uses `swa-cli.config.json`. It publishes the WASM app, starts the Functions API, and serves
everything at **http://localhost:4280**. Click **Sign in with GitHub** → the SWA emulator shows a
fake-login form (type any username/roles) → the whoami card reflects that identity.

> If `swa start` complains about the build/output path on first run, build manually and point the
> CLI at the output:
> ```powershell
> dotnet publish Warhammer40k.11/Warhammer40k.11.csproj -c Release
> swa start Warhammer40k.11/bin/Release/net10.0/publish/wwwroot --api-location Warhammer40k.Api
> ```

---

## B. Put the repository on GitHub  **[NOW for deploy]**

Static Web Apps' standard CI/CD deploys **from GitHub**. If this repo isn't on GitHub yet:

```powershell
cd C:\Users\tobhmerl\source\repos\Warhammer40k.11
git init
git add .
git commit -m "M0: cloud-ready solution skeleton"
# create an empty repo on github.com first, then:
git remote add origin https://github.com/<you>/Warhammer40k.11.git
git branch -M main
git push -u origin main
```
A root `.gitignore` is included so `bin/`, `obj/`, and `local.settings.json` are not committed.

---

## C. Azure Portal — create the Static Web App  **[NOW for deploy]**

1. https://portal.azure.com → **Create a resource** → search **Static Web App** → Create.
2. Fill in:
   - **Subscription**: your subscription.
   - **Resource Group**: Create new, e.g. `rg-tombforge`.
   - **Name**: e.g. `tombforge`.
   - **Plan type**: **Free**.
   - **Region**: nearest to you.
   - **Deployment source**: **GitHub** → authorize → pick **Organization / Repository / Branch = main**.
3. **Build Details**:
   - **Build Presets**: **Blazor**
   - **App location**: `Warhammer40k.11`
   - **Api location**: `Warhammer40k.Api`
   - **Output location**: `wwwroot`
4. **Review + create**. Azure commits a workflow file
   (`.github/workflows/azure-static-web-apps-*.yml`) to your repo and runs the first deploy.
5. When the GitHub Action finishes, your site is live at **https://&lt;name&gt;.azurestaticapps.net**.

### ⚠️ If the build fails on the .NET 10 API (managed Functions lag)
The SWA *managed* Functions platform can trail the newest runtime. If the API step fails:
1. Create a standalone **Azure Functions app** (.NET isolated) and deploy `Warhammer40k.Api` to it.
2. In the SWA resource → **APIs** → **Link** the Functions app.
3. Remove `api_location` from the workflow (set it to `""`).

No code changes are needed — same `/api`, same login. (This was flagged during planning.)

---

## D. Authentication (GitHub login)  **[NOW basic / M1 hardening]**

- Built-in login works out of the box: `/.auth/login/github`, `/.auth/logout`,
  and `/.auth/me` (current principal). The API reads the user from the `x-ms-client-principal`
  header — already implemented in `WhoAmI.cs`.
- **Restrict to just you** (recommended in **M1**): your data is partitioned per user id, so a
  stranger who logs in only ever sees their own empty data. To block them entirely, add a route
  rule requiring an `authenticated`/custom role in `staticwebapp.config.json` and invite only your
  account via **SWA → Role management → Invite**.
- Optional branded login: register your own GitHub OAuth app and add `GITHUB_CLIENT_ID` /
  `GITHUB_CLIENT_SECRET` as SWA app settings. Not required for M0/M1.

---

## E. Azure Table Storage  **[M1]**

1. Portal → **Create a resource** → **Storage account**:
   - Resource group `rg-tombforge`, **Name** e.g. `tombforgestore` (globally unique, lowercase),
	 **Redundancy** LRS (cheapest).
2. After creation → **Security + networking → Access keys** → copy a **connection string**.
3. SWA resource → **Settings → Environment variables** → add
   **`TablesConnectionString`** = that connection string. (The API reads this in M1.)
   - If you used a *linked* Functions app (section C fallback), set it on that Functions app instead.
4. Local dev uses **Azurite** via `UseDevelopmentStorage=true` (already in
   `Warhammer40k.Api/local.settings.json`).

---

## F. Optional — custom domain  **[later]**

SWA resource → **Custom domains** → add your domain and follow the DNS validation steps.

---

## Quick checklist

**Do now (to run + deploy M0):**
- [ ] `npm i -g @azure/static-web-apps-cli`
- [ ] Push repo to GitHub (section B)
- [ ] Create the Static Web App + first deploy (section C)

**Do in M1 (persistence):**
- [ ] Create the Storage account + copy connection string (section E)
- [ ] Add `TablesConnectionString` app setting in SWA (section E)
- [ ] (Recommended) lock login down to your account (section D)
