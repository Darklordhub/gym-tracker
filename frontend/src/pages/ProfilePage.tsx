import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { changePassword, fetchProfile, updateProfile } from '../api/profile'
import { fetchCycleGuidance, fetchCycleSettings, updateCycleSettings } from '../api/cycle'
import { useAuth } from '../auth/AuthContext'
import { StateCard } from '../components/StateCard'
import { formatDate } from '../lib/format'
import { getRequestErrorMessage } from '../lib/http'
import type { CycleGuidance, CycleSettings } from '../types/cycle'
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

const emptyCycleSettings: CycleSettings = {
  isEnabled: false,
  lastPeriodStartDate: null,
  averageCycleLengthDays: null,
  averagePeriodLengthDays: null,
  cycleRegularity: 'regular',
  usesHormonalContraception: null,
  isNaturallyCycling: null,
  updatedAt: null,
  isSetupComplete: false,
  canPredict: false,
  setupMessage: null,
}

export function ProfilePage() {
  const { authState, setCurrentUser } = useAuth()
  const [profile, setProfile] = useState<UserProfile | null>(authState?.user ?? null)
  const [profileForm, setProfileForm] = useState<ProfileFormState>(() =>
    authState?.user ? mapProfileToForm(authState.user) : emptyProfileForm,
  )
  const [passwordForm, setPasswordForm] = useState<PasswordFormState>(emptyPasswordForm)
  const [cycleSettings, setCycleSettings] = useState<CycleSettings>(emptyCycleSettings)
  const [cycleGuidance, setCycleGuidance] = useState<CycleGuidance | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isSavingProfile, setIsSavingProfile] = useState(false)
  const [isChangingPassword, setIsChangingPassword] = useState(false)
  const [isSavingCycleToggle, setIsSavingCycleToggle] = useState(false)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [profileError, setProfileError] = useState<string | null>(null)
  const [profileMessage, setProfileMessage] = useState<string | null>(null)
  const [passwordError, setPasswordError] = useState<string | null>(null)
  const [passwordMessage, setPasswordMessage] = useState<string | null>(null)
  const [cycleError, setCycleError] = useState<string | null>(null)
  const [cycleMessage, setCycleMessage] = useState<string | null>(null)

  useEffect(() => {
    void loadProfile()
  }, [])

  async function loadProfile() {
    try {
      setIsLoading(true)
      setLoadError(null)

      const [currentProfile, currentCycleSettings, currentCycleGuidance] = await Promise.all([
        fetchProfile(),
        fetchCycleSettings().catch(() => emptyCycleSettings),
        fetchCycleGuidance().catch(() => null),
      ])

      setProfile(currentProfile)
      setProfileForm(mapProfileToForm(currentProfile))
      setCurrentUser(currentProfile)
      setCycleSettings(currentCycleSettings)
      setCycleGuidance(currentCycleGuidance)
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

  async function handleCycleToggleChange(nextIsEnabled: boolean) {
    try {
      setIsSavingCycleToggle(true)
      setCycleError(null)
      setCycleMessage(null)

      const savedSettings = await updateCycleSettings({
        ...cycleSettings,
        isEnabled: nextIsEnabled,
      })

      setCycleSettings(savedSettings)
      if (nextIsEnabled) {
        const nextGuidance = await fetchCycleGuidance().catch(() => null)
        setCycleGuidance(nextGuidance)
      } else {
        setCycleGuidance(null)
      }

      window.dispatchEvent(new CustomEvent('cycle-settings-updated', { detail: { isEnabled: savedSettings.isEnabled } }))
      setCycleMessage(
        savedSettings.isEnabled
          ? savedSettings.setupMessage ?? 'Cycle-aware guidance enabled. You can manage the full feature from the Cycle page.'
          : 'Cycle-aware guidance disabled. Cycle-specific guidance and navigation are now hidden.',
      )
    } catch (error) {
      setCycleError(getRequestErrorMessage(error, 'Unable to update cycle-aware guidance.'))
    } finally {
      setIsSavingCycleToggle(false)
    }
  }

  return (
    <main className="page-shell">
      <section className="profile-hero-forge">
        <div className="profile-hero-main">
          <span className="eyebrow">FORGE / Account</span>
          <h1>Profile</h1>
          <p className="hero-text">
            Keep your personal details current, manage password security, and decide whether cycle-aware guidance should be part of your experience.
          </p>
        </div>

        <div className="profile-hero-side">
          <article className="forge-focus-card">
            <span className="stat-label">Account state</span>
            <strong>{cycleSettings.isEnabled ? 'Cycle-aware mode active' : 'Core profile only'}</strong>
            <p>
              {cycleSettings.isEnabled
                ? cycleGuidance?.guidanceHeadline ?? 'Dedicated cycle tracking is available.'
                : 'Cycle-specific guidance and navigation stay hidden until you opt in.'}
            </p>
            <div className="forge-focus-pills">
              <span className="info-pill">{profile?.displayName || profile?.fullName || 'No display name'}</span>
              <span className="info-pill info-pill-strength">{profile ? formatDate(profile.createdAt) : 'Loading...'}</span>
            </div>
          </article>
        </div>
      </section>

      <section className="forge-stat-strip forge-stat-strip-profile">
        <article className="forge-stat-card forge-stat-card-blue">
          <div className="forge-stat-glow" aria-hidden="true" />
          <span className="stat-label">Account email</span>
          <strong>{profile?.email ?? authState?.user.email ?? 'Loading...'}</strong>
          <p>Your sign-in identifier stays read-only here.</p>
        </article>
        <article className="forge-stat-card forge-stat-card-teal">
          <div className="forge-stat-glow" aria-hidden="true" />
          <span className="stat-label">Member since</span>
          <strong>{profile ? formatDate(profile.createdAt) : 'Loading...'}</strong>
          <p>Created automatically when your account was registered.</p>
        </article>
        <article className="forge-stat-card forge-stat-card-violet">
          <div className="forge-stat-glow" aria-hidden="true" />
          <span className="stat-label">Display name</span>
          <strong>{profile?.displayName || profile?.fullName || 'Not set'}</strong>
          <p>Used as your preferred label inside the app.</p>
        </article>
        <article className="forge-stat-card forge-stat-card-lime">
          <div className="forge-stat-glow" aria-hidden="true" />
          <span className="stat-label">Cycle feature</span>
          <strong>{cycleSettings.isEnabled ? 'Enabled' : 'Off'}</strong>
          <p>
            {cycleSettings.isEnabled
              ? cycleGuidance?.guidanceHeadline ?? 'Dedicated cycle tracking is available.'
              : 'Hidden until you choose to turn it on.'}
          </p>
        </article>
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
        <section className="profile-main-grid">
          <div className="panel">
            <div className="panel-header">
              <div>
                <h2>Personal Details</h2>
                <p>Update the personal information tied to your account. Sign-in email stays read-only.</p>
              </div>
            </div>

            <form className="weight-form profile-form-panel profile-form-forge" onSubmit={handleProfileSubmit}>
              <p className="section-note">
                Full name is required. Everything else here is optional and can be updated later.
              </p>

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
                    onChange={(event) => setProfileForm((current) => ({ ...current, fullName: event.target.value }))}
                    required
                  />
                  <small>Used as your primary account identity.</small>
                </label>

                <label className="field">
                  <span>Display name</span>
                  <input
                    type="text"
                    value={profileForm.displayName}
                    maxLength={80}
                    onChange={(event) => setProfileForm((current) => ({ ...current, displayName: event.target.value }))}
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
                    onChange={(event) => setProfileForm((current) => ({ ...current, dateOfBirth: event.target.value }))}
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
                    onChange={(event) => setProfileForm((current) => ({ ...current, heightCm: event.target.value }))}
                    placeholder="Optional"
                  />
                </label>

                <label className="field field-span-2">
                  <span>Gender</span>
                  <input
                    type="text"
                    value={profileForm.gender}
                    maxLength={50}
                    onChange={(event) => setProfileForm((current) => ({ ...current, gender: event.target.value }))}
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

            {profileMessage || profileError ? (
              <div className="feedback-stack">
                {profileMessage ? <p className="feedback success">{profileMessage}</p> : null}
                {profileError ? <p className="feedback error">{profileError}</p> : null}
              </div>
            ) : null}
          </div>

          <div className="panel">
            <div className="panel-header">
              <div>
                <h2>Security</h2>
                <p>Change your password here without affecting the rest of your profile information.</p>
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

            <form className="weight-form profile-form-panel profile-form-forge" onSubmit={handlePasswordSubmit}>
              <p className="section-note">
                Use a password you do not reuse elsewhere and confirm it before saving.
              </p>

              <label className="field">
                <span>Current password</span>
                <input
                  type="password"
                  autoComplete="current-password"
                  value={passwordForm.currentPassword}
                  onChange={(event) => setPasswordForm((current) => ({ ...current, currentPassword: event.target.value }))}
                  minLength={8}
                  required
                />
                <small>Enter your current password before choosing a new one.</small>
              </label>

              <label className="field">
                <span>New password</span>
                <input
                  type="password"
                  autoComplete="new-password"
                  value={passwordForm.newPassword}
                  onChange={(event) => setPasswordForm((current) => ({ ...current, newPassword: event.target.value }))}
                  minLength={8}
                  required
                />
                <small>Choose at least 8 characters.</small>
              </label>

              <label className="field">
                <span>Confirm new password</span>
                <input
                  type="password"
                  autoComplete="new-password"
                  value={passwordForm.confirmPassword}
                  onChange={(event) => setPasswordForm((current) => ({ ...current, confirmPassword: event.target.value }))}
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

            {passwordMessage || passwordError ? (
              <div className="feedback-stack">
                {passwordMessage ? <p className="feedback success">{passwordMessage}</p> : null}
                {passwordError ? <p className="feedback error">{passwordError}</p> : null}
              </div>
            ) : null}
          </div>

          <div className="panel panel-span-2 profile-cycle-panel">
            <div className="panel-header">
              <div>
                <h2>Cycle-Aware Training</h2>
                <p>Keep this optional. When enabled, the app adds a dedicated Cycle page and surfaces cycle-aware training guidance where it is useful.</p>
              </div>
            </div>

            <div className="profile-form-panel cycle-toggle-panel">
              <div className="toggle-row">
                <div>
                  <strong>Enable cycle-aware training guidance</strong>
                  <p className="muted-note">
                    Turn this on only if you want cycle-aware prediction, symptom logging, and training context in the app.
                  </p>
                </div>
                <label className="toggle-control">
                  <input
                    type="checkbox"
                    checked={cycleSettings.isEnabled}
                    onChange={(event) => void handleCycleToggleChange(event.target.checked)}
                    disabled={isSavingCycleToggle}
                  />
                  <span>{cycleSettings.isEnabled ? 'On' : 'Off'}</span>
                </label>
              </div>

              <div className="assistant-grid cycle-overview-grid">
                <article className="assistant-card">
                  <span className="stat-label">Visibility</span>
                  <strong>{cycleSettings.isEnabled ? 'Cycle page available' : 'Hidden from navigation'}</strong>
                  <p>
                    {cycleSettings.isEnabled
                      ? 'The dedicated Cycle page is now available for settings, period history, symptom logs, and guidance.'
                      : 'Cycle-specific guidance and navigation stay hidden until you opt in.'}
                  </p>
                </article>

                <article className="assistant-card">
                  <span className="stat-label">Current guidance</span>
                  <strong>
                    {cycleSettings.canPredict
                      ? cycleGuidance?.estimatedCurrentPhase ?? 'Building estimate'
                      : 'Setup incomplete'}
                  </strong>
                  <p>
                    {cycleSettings.isEnabled
                      ? cycleSettings.setupMessage ?? cycleGuidance?.guidanceMessage ?? 'Finish your cycle setup on the Cycle page to get more useful guidance.'
                      : 'No cycle-aware training guidance is shown across the app while the feature is off.'}
                  </p>
                </article>
              </div>

              {cycleSettings.isEnabled ? (
                <p className="section-note">
                  Full setup, period history, symptom logging, and personalized guidance now live on the <Link to="/cycle">Cycle page</Link>.
                </p>
              ) : null}
            </div>

            {cycleMessage || cycleError ? (
              <div className="feedback-stack">
                {cycleMessage ? <p className="feedback success">{cycleMessage}</p> : null}
                {cycleError ? <p className="feedback error">{cycleError}</p> : null}
              </div>
            ) : null}
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
