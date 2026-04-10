import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { getRequestErrorMessage } from '../lib/http'

type RegisterFormState = {
  email: string
  password: string
  confirmPassword: string
}

export function RegisterPage() {
  const navigate = useNavigate()
  const { register } = useAuth()
  const [form, setForm] = useState<RegisterFormState>({
    email: '',
    password: '',
    confirmPassword: '',
  })
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const isSubmitDisabled =
    isSubmitting ||
    form.email.trim() === '' ||
    form.password === '' ||
    form.confirmPassword === ''

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (form.password !== form.confirmPassword) {
      setErrorMessage('Passwords do not match.')
      return
    }

    try {
      setIsSubmitting(true)
      setErrorMessage(null)
      await register({
        email: form.email.trim(),
        password: form.password,
      })
      navigate('/dashboard', { replace: true })
    } catch (error) {
      setErrorMessage(getRequestErrorMessage(error, 'Unable to register this account.'))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <main className="auth-shell">
      <section className="auth-card">
        <div className="auth-copy">
          <span className="eyebrow">Gym Tracker</span>
          <h1>Create account</h1>
          <p className="hero-text">
            Create your personal account to track workouts, body weight, and progress in one place.
          </p>
          <p className="auth-supporting-text">
            After sign-in, each account only sees its own data.
          </p>
        </div>

        <form className="weight-form auth-form" onSubmit={handleSubmit} noValidate>
          <label className="field">
            <span>Email address</span>
            <input
              type="email"
              autoComplete="email"
              inputMode="email"
              value={form.email}
              onChange={(event) => {
                setForm((current) => ({ ...current, email: event.target.value }))
                if (errorMessage) {
                  setErrorMessage(null)
                }
              }}
              placeholder="you@example.com"
              aria-invalid={Boolean(errorMessage)}
              required
            />
            <small>This email will be used to sign in.</small>
          </label>

          <label className="field">
            <span>Password</span>
            <input
              type="password"
              autoComplete="new-password"
              value={form.password}
              onChange={(event) => {
                setForm((current) => ({ ...current, password: event.target.value }))
                if (errorMessage) {
                  setErrorMessage(null)
                }
              }}
              minLength={8}
              placeholder="Create a password"
              aria-invalid={Boolean(errorMessage)}
              required
            />
            <small>Use at least 8 characters.</small>
          </label>

          <label className="field">
            <span>Confirm password</span>
            <input
              type="password"
              autoComplete="new-password"
              value={form.confirmPassword}
              onChange={(event) => {
                setForm((current) => ({ ...current, confirmPassword: event.target.value }))
                if (errorMessage) {
                  setErrorMessage(null)
                }
              }}
              minLength={8}
              placeholder="Repeat your password"
              aria-invalid={Boolean(errorMessage)}
              required
            />
            <small>Enter the same password again to confirm it.</small>
          </label>

          <button type="submit" className="primary-button auth-submit-button" disabled={isSubmitDisabled}>
            {isSubmitting ? 'Creating account...' : 'Create account'}
          </button>
        </form>

        {errorMessage ? <p className="feedback error auth-feedback">{errorMessage}</p> : null}

        <p className="auth-footer">
          Already have an account? <Link to="/login">Log in</Link>
        </p>
      </section>
    </main>
  )
}
