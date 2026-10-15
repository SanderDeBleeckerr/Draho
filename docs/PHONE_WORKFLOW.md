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

### A. Unity license file (needs a computer one time, about 15 minutes)

Unity does not let you activate a free Personal license from the phone, so you need
a computer for this step. A Mac or PC that you can borrow is enough. You do **not**
need the Unity Editor or Xcode.

1. Install **Unity Hub** from <https://unity.com/download>.
2. Sign in with your Unity account. Make a free account if you do not have one.
3. Go to **Preferences → Licenses → Add → Get a free personal license**.
4. Open the license file and copy all the text in it:
   - Mac: `/Library/Application Support/Unity/Unity_lic.ulf`
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
5. Send the text to your phone (for example, with a note to yourself). You need it in step B.

### B. GitHub secrets (from the phone browser)

Go to **github.com → Draho → Settings → Secrets and variables → Actions →
New repository secret**. Add these three secrets:

| Name | Value |
|---|---|
| `UNITY_LICENSE` | All the text of `Unity_lic.ulf` |
| `UNITY_EMAIL` | The email of your Unity account |
| `UNITY_PASSWORD` | The password of your Unity account. GameCI warns that special characters can cause problems. |

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
