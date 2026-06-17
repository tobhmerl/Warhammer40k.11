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

> **API/Core target `net9.0`; the UI targets `net10.0`.** `Warhammer40k.Api` and
> `Warhammer40k.Core` are pinned to .NET 9 so the SWA Free-plan managed Functions builder accepts
> them (see section C). Building works with the .NET 10 SDK alone, but to **run the API locally**
> (`func` / `swa start`) you need the **.NET 9 (ASP.NET Core) runtime** installed
> (`winget install Microsoft.DotNet.AspNetCore.9`) — otherwise just let Azure host it on deploy.

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

## B. Put the repository on GitHub (manual)  **[NOW for deploy]**

Static Web Apps' standard CI/CD deploys **from GitHub**. The repo is already initialized
locally (branch `main`, commit `M0: cloud-ready solution skeleton`), so you only need to create
the **empty** GitHub repo by hand and push to it. No `git init` / CLI repo-creation step is needed.

### B1. Create the empty repo on github.com
1. Open **https://github.com/new**.
2. **Owner**: your account (`tobhmerl`).  **Repository name**: `Warhammer40k.11`.
3. Visibility: **Private** (recommended) or Public.
4. **Do NOT** tick *Add a README*, *.gitignore*, or *license* — the repo already has these
   locally, and initializing the remote would make the first push fail with a non-fast-forward error.
5. Click **Create repository** and leave the "…push an existing repository" page open for reference.

### B2. Point the local repo at it and push
The local `origin` previously contained a `<you>` placeholder. It has been corrected to the URL
below — replace `tobhmerl` only if your GitHub username differs:

```powershell
cd C:\Users\tobhmerl\source\repos\Warhammer40k.11
git remote set-url origin https://github.com/tobhmerl/Warhammer40k.11.git
git remote -v                      # confirm the URL no longer shows <you>
git push -u origin main
```

> The first push opens a browser / Git Credential Manager prompt — sign in to GitHub (or use a PAT).
> If `origin` does not exist, use `git remote add origin <url>` instead of `set-url`.

A root `.gitignore` is included so `bin/`, `obj/`, and `local.settings.json` are not committed.

✅ Once `git push` succeeds and your files appear on github.com, continue to **section C** to create
the Static Web App.

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

### ⚠️ The .NET 10 API vs SWA managed Functions — RESOLVED by pinning to .NET 9
The SWA **managed** Functions builder only accepts `dotnetisolated` **8.0/9.0**. A **net10.0** API
fails the deploy with: *"The function language version detected is unsupported or invalid. The
following dotnetisolated language versions are valid: 8.0, 9.0."*

**What we did (Free plan, $0):** pinned **`Warhammer40k.Api`** and **`Warhammer40k.Core`** to
**`net9.0`** so the managed builder accepts them. The Blazor WASM UI (**`Warhammer40k.11`**) stays on
**`net10.0`** and consumes the net9 `Core` without issue. No workflow edit is needed — `api_location`
still points at `Warhammer40k.Api`, and the builder detects 9.0 from the csproj.

> A clean **Debug** solution build also required removing the
> `<Build Solution="Debug|*" Project="false" />` overrides from `Warhammer40k.11.slnx`, which were
> excluding `Core`/`Api`/`Tests` from building in Debug.

**Revert to .NET 10 later:** once the SWA managed builder supports .NET 10, bump `<TargetFramework>`
back to `net10.0` in `Warhammer40k.Core/Warhammer40k.Core.csproj` and
`Warhammer40k.Api/Warhammer40k.Api.csproj`.

**Alternative — keep .NET 10 now (~$9/mo):** host the API as a standalone **Flex Consumption**
Function App (supports .NET 8/9/10), upgrade the SWA to the **Standard** plan, set `api_location: ""`
in the workflow, then **APIs → Link** the Function App. Bring-your-own/linked backends require the
Standard plan. Same `/api`, same built-in auth.

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
