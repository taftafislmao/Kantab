import './style.css'
import { supabase, isConfigured } from './supabase'

type Page = 'home' | 'sign-in' | 'sign-up'

function getPage(): Page {
  const p = location.pathname
  if (p.startsWith('/sign-in')) return 'sign-in'
  if (p.startsWith('/sign-up')) return 'sign-up'
  return 'home'
}

function navigate(path: string) {
  history.pushState(null, '', path)
  render()
}

window.addEventListener('popstate', render)

function escapeHtml(s: string): string {
  return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;')
}

async function render() {
  const page = getPage()
  const app = document.querySelector<HTMLDivElement>('#app')!

  let session = null as any
  if (isConfigured && supabase) {
    try { const { data } = await supabase.auth.getSession(); session = data?.session } catch {}
  }

  const header = `
    <header style="display:flex;justify-content:space-between;align-items:center;padding:14px 20px;border-bottom:1px solid var(--border);gap:12px">
      <a href="/" style="text-decoration:none;color:var(--text-h);font-weight:600;letter-spacing:0.2px">KanTab</a>
      <nav style="display:flex;gap:10px;align-items:center">
        ${session ? `<span style="font-size:13px;color:var(--text)">${escapeHtml(session.user.email ?? '')}</span><button id="signout-btn" style="padding:6px 12px;border-radius:6px;border:1px solid var(--border);background:var(--bg);color:var(--text-h);cursor:pointer">Sign out</button>` 
          : `<a href="/sign-in" style="text-decoration:none;color:var(--text-h);font-size:14px">Sign in</a><a href="/sign-up" style="text-decoration:none;background:var(--accent);color:#fff;padding:6px 14px;border-radius:6px;font-size:14px">Sign up</a>`}
      </nav>
    </header>`

  if (page === 'home') {
    app.innerHTML = header + `
      <main style="max-width:560px;margin:40px auto;padding:0 20px">
        <h1 style="font-size:28px;margin:0 0 8px">KanTab</h1>
        <p style="margin:0 0 20px">A simple place to organize your work. ${session ? 'You are signed in.' : 'Create an account or sign in to get started.'}</p>
        ${!session ? `<div style="display:flex;gap:10px">
          <a href="/sign-in" style="text-decoration:none;border:1px solid var(--border);padding:8px 16px;border-radius:8px;color:var(--text-h)">Sign in</a>
          <a href="/sign-up" style="text-decoration:none;background:var(--accent);color:#fff;padding:8px 16px;border-radius:8px">Sign up</a>
        </div>` : ''}
        ${!isConfigured ? `<p style="margin-top:16px;padding:10px;border:1px solid #f0c040;background:#fff8e1;border-radius:8px;color:#6b5900;font-size:13px">Supabase is not configured. Set <code>VITE_SUPABASE_URL</code> and <code>VITE_SUPABASE_ANON_KEY</code>.</p>` : ''}
      </main>`
    document.getElementById('signout-btn')?.addEventListener('click', async () => {
      try { await supabase?.auth.signOut() } catch {}
      render()
    })
    return
  }

  const isSignUp = page === 'sign-up'
  const title = isSignUp ? 'Create account' : 'Sign in'
  const actionLabel = isSignUp ? 'Sign up' : 'Sign in'

  app.innerHTML = header + `
    <main style="max-width:420px;margin:32px auto;padding:0 20px">
      <h1 style="font-size:22px;margin:0 0 6px">${title}</h1>
      <p style="margin:0 0 14px;font-size:13px;color:var(--text)">${isSignUp ? 'Create your KanTab account.' : 'Welcome back.'}
        ${isSignUp ? `Already have an account? <a href="/sign-in">Sign in</a>` : `No account? <a href="/sign-up">Sign up</a>`}
      </p>
      ${!isConfigured ? `<p style="padding:10px;border:1px solid #f0c040;background:#fff8e1;border-radius:8px;color:#6b5900;font-size:13px">Supabase is not configured.</p>` : ''}
      <form id="auth-form" style="display:flex;flex-direction:column;gap:10px;margin-top:10px">
        <label style="font-size:12px;color:var(--text)">Email
          <input id="email" type="email" required autocomplete="email" placeholder="you@example.com"
            style="width:100%;margin-top:4px;padding:10px 12px;border:1px solid var(--border);border-radius:8px;background:var(--bg);color:var(--text-h);box-sizing:border-box" />
        </label>
        <label style="font-size:12px;color:var(--text)">Password
          <input id="password" type="password" required autocomplete="${isSignUp ? 'new-password' : 'current-password'}" placeholder="••••••••"
            style="width:100%;margin-top:4px;padding:10px 12px;border:1px solid var(--border);border-radius:8px;background:var(--bg);color:var(--text-h);box-sizing:border-box" />
        </label>
        ${isSignUp ? `
        <label style="font-size:12px;color:var(--text)">Confirm password
          <input id="confirm" type="password" required autocomplete="new-password" placeholder="••••••••"
            style="width:100%;margin-top:4px;padding:10px 12px;border:1px solid var(--border);border-radius:8px;background:var(--bg);color:var(--text-h);box-sizing:border-box" />
        </label>` : ''}
        <p id="auth-error" style="display:none;margin:4px 0 0;padding:8px 10px;background:#fde8e8;border:1px solid #f5b5b5;border-radius:8px;color:#8a1a1a;font-size:12px"></p>
        <p id="auth-ok" style="display:none;margin:4px 0 0;padding:8px 10px;background:#e8f5e9;border:1px solid #a5d6a7;border-radius:8px;color:#1b5e20;font-size:12px"></p>
        <button id="auth-submit" type="submit" style="margin-top:4px;padding:10px;border:none;border-radius:8px;background:var(--accent);color:#fff;font-weight:600;cursor:pointer">${actionLabel}</button>
        ${isSignUp ? '' : `<a href="/sign-up" style="text-align:center;font-size:13px;margin-top:2px">Create account</a>`}
      </form>
    </main>`

  // SPA navigation links
  app.querySelectorAll('a[href^="/"]').forEach(a => {
    a.addEventListener('click', e => {
      const href = (a as HTMLAnchorElement).getAttribute('href')!
      if (href.startsWith('/')) { e.preventDefault(); navigate(href) }
    })
  })

  document.getElementById('signout-btn')?.addEventListener('click', async () => {
    try { await supabase?.auth.signOut() } catch {}
    render()
  })

  const form = document.getElementById('auth-form') as HTMLFormElement | null
  const errEl = document.getElementById('auth-error') as HTMLParagraphElement | null
  const okEl = document.getElementById('auth-ok') as HTMLParagraphElement | null
  const submitBtn = document.getElementById('auth-submit') as HTMLButtonElement | null

  function showError(msg: string) {
    if (!errEl) return
    errEl.textContent = msg
    errEl.style.display = 'block'
    if (okEl) okEl.style.display = 'none'
  }
  function showOk(msg: string) {
    if (!okEl) return
    okEl.textContent = msg
    okEl.style.display = 'block'
    if (errEl) errEl.style.display = 'none'
  }
  function clearMessages() {
    if (errEl) { errEl.textContent = ''; errEl.style.display = 'none' }
    if (okEl) { okEl.textContent = ''; okEl.style.display = 'none' }
  }
  function friendlyError(e: any): string {
    const msg = (e?.message ?? String(e ?? '')).toLowerCase()
    if (msg.includes('invalid login credentials')) return 'Invalid email or password.'
    if (msg.includes('already registered') || msg.includes('already exists') || msg.includes('user already registered')) return 'An account with this email already exists.'
    if (msg.includes('password')) {
      if (msg.includes('weak') || msg.includes('at least') || msg.includes('short')) return 'Password is too weak. Use at least 6 characters.'
    }
    if (msg.includes('confirm')) return 'Please confirm your email before signing in.'
    if (msg.includes('network') || msg.includes('fetch') || msg.includes('failed to fetch')) return 'Network unavailable. Please check your connection.'
    if (msg.includes('not configured')) return 'Supabase is not configured.'
    return e?.message ?? 'Something went wrong. Please try again.'
  }

  form?.addEventListener('submit', async e => {
    e.preventDefault()
    clearMessages()
    const email = (document.getElementById('email') as HTMLInputElement).value.trim()
    const password = (document.getElementById('password') as HTMLInputElement).value
    const confirm = (document.getElementById('confirm') as HTMLInputElement | null)?.value ?? ''

    if (!email || !email.includes('@')) { showError('Please enter a valid email address.'); return }
    if (!password || password.length < 6) { showError('Password must be at least 6 characters.'); return }
    if (isSignUp && password !== confirm) { showError('Passwords do not match.'); return }
    if (!isConfigured || !supabase) { showError('Supabase is not configured.'); return }

    if (submitBtn) { submitBtn.disabled = true; submitBtn.textContent = 'Please wait…' }

    try {
      if (isSignUp) {
        const { data, error } = await supabase.auth.signUp({ email, password })
        if (error) { showError(friendlyError(error)); return }
        // If email confirmation required, there is no session yet.
        if (!data.session) {
          showOk('Check your email to confirm your account.')
          return
        }
        showOk('Account created. You are signed in.')
        setTimeout(() => navigate('/'), 600)
      } else {
        const { error } = await supabase.auth.signInWithPassword({ email, password })
        if (error) { showError(friendlyError(error)); return }
        showOk('Signed in.')
        setTimeout(() => navigate('/'), 400)
      }
    } catch (ex: any) {
      showError(friendlyError(ex))
    } finally {
      if (submitBtn) { submitBtn.disabled = false; submitBtn.textContent = actionLabel }
    }
  })
}

render()
