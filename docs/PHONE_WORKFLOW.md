# Working from a phone

You can develop Draho with only an iPhone. Claude writes the code and the
cat model. GitHub builds the game, and you play it in Safari.

```
Phone (Claude app)  ──►  push to main  ──►  GitHub Actions (GameCI, Unity 6000.3.26f1)
                                                  │  WebGL build, about 15–30 min
                                                  ▼
                              https://<user>.github.io/Draho/  ──►  Safari on iPhone
```

## One-time setup

### A. Unity account (from the phone browser)

The build server (a fresh GitHub VM for each build) activates the free Unity Personal
license with your Unity email and password. You do not need a computer.

1. Make a **new Unity account only for builds** on <https://id.unity.com>. Do not use your
   main account. Sign in once on unity.com, so the account accepts the Unity terms.
2. **Switch off two-step verification** for this account. A build server cannot type a code.
3. Use a password with only letters and numbers. GameCI warns that special characters can cause problems.

> This method is **experimental**. GameCI added it in September 2026 (game-ci/cli PR #246),
> and it uses Unity licensing flags that Unity does not document. The build script always
> returns the license seat at the end, also when the build fails.

### B. GitHub secrets (from the phone browser)

Go to **github.com → Draho → Settings → Secrets and variables → Actions →
New repository secret**. Add these secrets:

| Name | Value |
|---|---|
| `UNITY_EMAIL` | The email of the build-only Unity account |
| `UNITY_PASSWORD` | Its password |
| `UNITY_LICENSE` | *Optional backup.* The text of a `Unity_lic.ulf` file. Use it only if the email + password method fails. You need a computer with Unity Hub one time to make this file (Preferences → Licenses → Add → free personal license). |

Secrets are encrypted. Claude cannot read them, and other people cannot see them.

### C. GitHub Pages (from the phone browser)

Go to **Settings → Pages → Build and deployment → Source** and select **GitHub Actions**.

## Every day

1. Ask Claude for a change. Claude pushes it to a branch.
2. Tell Claude to merge it into `main`, or merge the pull request in the GitHub app.
3. Wait for the **Web build (play on phone)** workflow to finish. You can see it in the GitHub app under **Actions**.
4. Open `https://<your-github-user>.github.io/Draho/` in Safari. Tap **Share → Add to Home Screen**.
   The game then opens in full screen, like an app.

To start a build without a change, go to **Actions → Web build (play on phone) → Run workflow**.

## Limits

- **The first build is slow.** The first build takes about 30 minutes, because Unity imports everything.
  Later builds reuse the cache and are faster.
- **Web performance is lower.** A web build runs slower than a native app. It is good for testing the feel and
  the controls, but not for the final performance test.
- **The web build is public.** For a public repository, GitHub Actions minutes are free, but GitHub Pages
  sites are public. Anyone with the link can play the build.
- **A native iPhone app comes later.** For TestFlight we need an Apple Developer account
  ($99 per year) and a macOS runner. That is a later step and can also run from GitHub Actions.
