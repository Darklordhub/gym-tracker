import { useEffect, useState, type FormEvent } from 'react'
import { changePassword, fetchProfile, updateProfile } from '../api/profile'
import { useAuth } from '../auth/AuthContext'
import { StateCard } from '../components/StateCard'
import { formatDate } from '../lib/format'
import { getRequestErrorMessage } from '../lib/http'
import type { UserProfile } from '../types/profile'

type ProfileFormState = {
  fullName: string
  displayName: string
  dateOfBirth: string
  heightCm: string
  gender: string
}

type PasswordFormState = {
  currentPassword: string
  newPassword: string
  confirmPassword: string
}

const emptyProfileForm: ProfileFormState = {
  fullName: '',
  displayName: '',
  dateOfBirth: '',
  heightCm: '',
  gender: '',
}

const emptyPasswordForm: PasswordFormState = {
  currentPassword: '',
  newPassword: '',
  confirmPassword: '',
}

export function ProfilePage() {
  const { authState, setCurrentUser } = useAuth()
  const [profile, setProfile] = useState<UserProfile | null>(authState?.user ?? null)
  const [profileForm, setProfileForm] = useState<ProfileFormState>(() =>
    authState?.user ? mapProfileToForm(authState.user) : emptyProfileForm,
  )
  const [passwordForm, setPasswordForm] = useState<PasswordFormState>(emptyPasswordForm)
  const [isLoading, setIsLoading] = useState(true)
  const [isSavingProfile, setIsSavingProfile] = useState(false)
  const [isChangingPassword, setIsChangingPassword] = useState(false)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [profileError, setProfileError] = useState<string | null>(null)
  const [profileMessage, setProfileMessage] = useState<string | null>(null)
  const [passwordError, setPasswordError] = useState<string | null>(null)
  const [passwordMessage, setPasswordMessage] = useState<string | null>(null)

  useEffect(() => {
    void loadProfile()
  }, [])

  async function loadProfile() {
    try {
      setIsLoading(true)
      setLoadError(null)

      const currentProfile = await fetchProfile()
      setProfile(currentProfile)
      setProfileForm(mapProfileToForm(currentProfile))
      setCurrentUser(currentProfile)
    } catch (error) {
      setLoadError(getRequestErrorMessage(error, 'Unable to load your profile.'))
    } finally {
      setIsLoading(false)
    }
  }

  async function handleProfileSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const validationMessage = validateProfileForm(profileForm)
    if (validationMessage) {
      setProfileError(validationMessage)
      setProfileMessage(null)
      return
    }

    try {
      setIsSavingProfile(true)
      setProfileError(null)
      setProfileMessage(null)

      const savedProfile = await updateProfile({
        fullName: profileForm.fullName.trim(),
        displayName: toOptionalValue(profileForm.displayName),
        dateOfBirth: toOptionalValue(profileForm.dateOfBirth),
        heightCm: profileForm.heightCm.trim() === '' ? null : Number(profileForm.heightCm),
        gender: toOptionalValue(profileForm.gender),
      })

      setProfile(savedProfile)
      setProfileForm(mapProfileToForm(savedProfile))
      setCurrentUser(savedProfile)
      setProfileMessage('Profile updated.')
    } catch (error) {
      setProfileError(getRequestErrorMessage(error, 'Unable to update your profile.'))
    } finally {
      setIsSavingProfile(false)
    }
  }

  async function handlePasswordSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (passwordForm.newPassword.length < 8) {
      setPasswordError('New password must be at least 8 characters long.')
      setPasswordMessage(null)
      return
    }

    if (passwordForm.newPassword !== passwordForm.confirmPassword) {
      setPasswordError('New password and confirmation do not match.')
      setPasswordMessage(null)
      return
    }

    if (passwordForm.currentPassword === passwordForm.newPassword) {
      setPasswordError('New password must be different from the current password.')
      setPasswordMessage(null)
      return
    }

    try {
      setIsChangingPassword(true)
      setPasswordError(null)
      setPasswordMessage(null)

      await changePassword({
        currentPassword: passwordForm.currentPassword,
        newPassword: passwordForm.newPassword,
      })

      setPasswordForm(emptyPasswordForm)
      setPasswordMessage('Password updated.')
    } catch (error) {
      setPasswordError(getRequestErrorMessage(error, 'Unable to update your password.'))
    } finally {
      setIsChangingPassword(false)
    }
  }

  return (
    <main className="page-shell">
      <section className="hero-panel">
        <div className="hero-copy">
          <span className="eyebrow">Account</span>
          <h1>Profile</h1>
          <p className="hero-text">
            Review your account details, keep your personal profile current, and manage your password.
          </p>
        </div>

        <div className="stats-grid">
          <article className="stat-card">
            <span className="stat-label">Account Email</span>
            <strong>{profile?.email ?? authState?.user.email ?? 'Loading...'}</strong>
            <span className="stat-subtext">Your sign-in identifier stays read-only here.</span>
          </article>
          <article className="stat-card">
            <span className="stat-label">Member Since</span>
            <strong>{profile ? formatDate(profile.createdAt) : 'Loading...'}</strong>
            <span className="stat-subtext">Created automatically when your account was registered.</span>
          </article>
          <article className="stat-card">
            <span className="stat-label">Display Name</span>
            <strong>{profile?.displayName || profile?.fullName || 'Not set'}</strong>
            <span className="stat-subtext">Used as your preferred label inside the app.</span>
          </article>
        </div>
      </section>

      {isLoading ? (
        <section className="content-grid profile-grid">
          <div className="panel panel-span-2">
            <StateCard title="Loading profile" description="Fetching your account details." loading />
          </div>
        </section>
      ) : loadError ? (
        <section className="content-grid profile-grid">
          <div className="panel panel-span-2">
            <StateCard title="Profile unavailable" description={loadError} tone="error" />
          </div>
        </section>
      ) : (
        <section className="content-grid profile-grid">
          <div className="panel">
            <div className="panel-header">
              <div>
                <h2>Personal Details</h2>
                <p>Update the profile information tied to your account.</p>
              </div>
            </div>

            <form className="weight-form" onSubmit={handleProfileSubmit}>
              <div className="form-grid">
                <label className="field field-span-2">
                  <span>Email</span>
                  <input type="email" value={profile?.email ?? ''} readOnly disabled />
                  <small>Sign-in email is managed separately and cannot be edited here.</small>
                </label>

                <label className="field">
                  <span>Full name</span>
                  <input
                    type="text"
                    value={profileForm.fullName}
                    maxLength={120}
                    onChange={(event) =>
                      setProfileForm((current) => ({ ...current, fullName: event.target.value }))
                    }
                    required
                  />
                </label>

                <label className="field">
                  <span>Display name</span>
                  <input
                    type="text"
                    value={profileForm.displayName}
                    maxLength={80}
                    onChange={(event) =>
                      setProfileForm((current) => ({ ...current, displayName: event.target.value }))
                    }
                    placeholder="Optional"
                  />
                  <small>Shown across the app when available.</small>
                </label>

                <label className="field">
                  <span>Date of birth</span>
                  <input
                    type="date"
                    value={profileForm.dateOfBirth}
                    max={new Date().toISOString().slice(0, 10)}
                    onChange={(event) =>
                      setProfileForm((current) => ({ ...current, dateOfBirth: event.target.value }))
                    }
                  />
                </label>

                <label className="field">
                  <span>Height (cm)</span>
                  <input
                    type="number"
                    min="50"
                    max="300"
                    step="1"
                    value={profileForm.heightCm}
                    onChange={(event) =>
                      setProfileForm((current) => ({ ...current, heightCm: event.target.value }))
                    }
                    placeholder="Optional"
                  />
                </label>

                <label className="field field-span-2">
                  <span>Gender</span>
                  <input
                    type="text"
                    value={profileForm.gender}
                    maxLength={50}
                    onChange={(event) =>
                      setProfileForm((current) => ({ ...current, gender: event.target.value }))
                    }
                    placeholder="Optional"
                  />
                </label>
              </div>

              <div className="action-row">
                <button type="submit" className="primary-button" disabled={isSavingProfile}>
                  {isSavingProfile ? 'Saving profile...' : 'Save profile'}
                </button>
              </div>
            </form>

            {profileMessage ? <p className="feedback success">{profileMessage}</p> : null}
            {profileError ? <p className="feedback error">{profileError}</p> : null}
          </div>

          <div className="panel">
            <div className="panel-header">
              <div>
                <h2>Security</h2>
                <p>Change your password without affecting the rest of the app.</p>
              </div>
            </div>

            <div className="profile-meta-grid">
              <div>
                <span className="stat-label">Created At</span>
                <strong>{profile ? formatDate(profile.createdAt) : 'Unknown'}</strong>
              </div>
              <div>
                <span className="stat-label">Preferred Name</span>
                <strong>{profile?.displayName || profile?.fullName || 'Not set'}</strong>
              </div>
            </div>

            <form className="weight-form" onSubmit={handlePasswordSubmit}>
              <label className="field">
                <span>Current password</span>
                <input
                  type="password"
                  autoComplete="current-password"
                  value={passwordForm.currentPassword}
                  onChange={(event) =>
                    setPasswordForm((current) => ({ ...current, currentPassword: event.target.value }))
                  }
                  minLength={8}
                  required
                />
              </label>

              <label className="field">
                <span>New password</span>
                <input
                  type="password"
                  autoComplete="new-password"
                  value={passwordForm.newPassword}
                  onChange={(event) =>
                    setPasswordForm((current) => ({ ...current, newPassword: event.target.value }))
                  }
                  minLength={8}
                  required
                />
              </label>

              <label className="field">
                <span>Confirm new password</span>
                <input
                  type="password"
                  autoComplete="new-password"
                  value={passwordForm.confirmPassword}
                  onChange={(event) =>
                    setPasswordForm((current) => ({ ...current, confirmPassword: event.target.value }))
                  }
                  minLength={8}
                  required
                />
              </label>

              <p className="muted-note">
                Use at least 8 characters. Your email stays unchanged on this page.
              </p>

              <div className="action-row">
                <button type="submit" className="primary-button" disabled={isChangingPassword}>
                  {isChangingPassword ? 'Updating password...' : 'Change password'}
                </button>
              </div>
            </form>

            {passwordMessage ? <p className="feedback success">{passwordMessage}</p> : null}
            {passwordError ? <p className="feedback error">{passwordError}</p> : null}
          </div>
        </section>
      )}
    </main>
  )
}

function mapProfileToForm(profile: UserProfile): ProfileFormState {
  return {
    fullName: profile.fullName,
    displayName: profile.displayName ?? '',
    dateOfBirth: profile.dateOfBirth ?? '',
    heightCm: profile.heightCm === null ? '' : profile.heightCm.toString(),
    gender: profile.gender ?? '',
  }
}

function toOptionalValue(value: string) {
  const trimmedValue = value.trim()
  return trimmedValue === '' ? null : trimmedValue
}

function validateProfileForm(form: ProfileFormState) {
  if (!form.fullName.trim()) {
    return 'Full name is required.'
  }

  if (form.heightCm.trim() !== '') {
    const heightCm = Number(form.heightCm)

    if (!Number.isInteger(heightCm) || heightCm < 50 || heightCm > 300) {
      return 'Height must be a whole number between 50 and 300 cm.'
    }
  }

  if (form.dateOfBirth && form.dateOfBirth > new Date().toISOString().slice(0, 10)) {
    return 'Date of birth cannot be in the future.'
  }

  return null
}
