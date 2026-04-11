import { useEffect, useState, type FormEvent } from 'react'
import {
  changePassword,
  fetchProfile,
  updateProfile,
} from '../api/profile'
import {
  createCycleEntry,
  deleteCycleEntry,
  fetchCycleGuidance,
  fetchCycleHistory,
  fetchCycleSettings,
  updateCycleEntry,
  updateCycleSettings,
} from '../api/cycle'
import { useAuth } from '../auth/AuthContext'
import { StateCard } from '../components/StateCard'
import { formatDate } from '../lib/format'
import { getRequestErrorMessage } from '../lib/http'
import type { CycleEntry, CycleGuidance, CycleRegularity, CycleSettings } from '../types/cycle'
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

type CycleSettingsFormState = {
  isEnabled: boolean
  lastPeriodStartDate: string
  averageCycleLengthDays: string
  averagePeriodLengthDays: string
  cycleRegularity: CycleRegularity
  usesHormonalContraception: 'yes' | 'no' | 'unknown'
  isNaturallyCycling: 'yes' | 'no' | 'unknown'
}

type CycleEntryFormState = {
  periodStartDate: string
  periodEndDate: string
  notes: string
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

const emptyCycleEntryForm: CycleEntryFormState = {
  periodStartDate: '',
  periodEndDate: '',
  notes: '',
}

const emptyCycleSettingsForm: CycleSettingsFormState = {
  isEnabled: false,
  lastPeriodStartDate: '',
  averageCycleLengthDays: '',
  averagePeriodLengthDays: '',
  cycleRegularity: 'regular',
  usesHormonalContraception: 'unknown',
  isNaturallyCycling: 'unknown',
}

export function ProfilePage() {
  const { authState, setCurrentUser } = useAuth()
  const [profile, setProfile] = useState<UserProfile | null>(authState?.user ?? null)
  const [profileForm, setProfileForm] = useState<ProfileFormState>(() =>
    authState?.user ? mapProfileToForm(authState.user) : emptyProfileForm,
  )
  const [passwordForm, setPasswordForm] = useState<PasswordFormState>(emptyPasswordForm)
  const [cycleSettingsForm, setCycleSettingsForm] = useState<CycleSettingsFormState>(emptyCycleSettingsForm)
  const [cycleGuidance, setCycleGuidance] = useState<CycleGuidance | null>(null)
  const [cycleHistory, setCycleHistory] = useState<CycleEntry[]>([])
  const [cycleEntryForm, setCycleEntryForm] = useState<CycleEntryFormState>(emptyCycleEntryForm)
  const [editingCycleEntryId, setEditingCycleEntryId] = useState<number | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isSavingProfile, setIsSavingProfile] = useState(false)
  const [isChangingPassword, setIsChangingPassword] = useState(false)
  const [isSavingCycleSettings, setIsSavingCycleSettings] = useState(false)
  const [isSavingCycleEntry, setIsSavingCycleEntry] = useState(false)
  const [deletingCycleEntryId, setDeletingCycleEntryId] = useState<number | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [profileError, setProfileError] = useState<string | null>(null)
  const [profileMessage, setProfileMessage] = useState<string | null>(null)
  const [passwordError, setPasswordError] = useState<string | null>(null)
  const [passwordMessage, setPasswordMessage] = useState<string | null>(null)
  const [cycleError, setCycleError] = useState<string | null>(null)
  const [cycleMessage, setCycleMessage] = useState<string | null>(null)
  const [cycleHistoryError, setCycleHistoryError] = useState<string | null>(null)
  const [cycleHistoryMessage, setCycleHistoryMessage] = useState<string | null>(null)

  useEffect(() => {
    void loadProfile()
  }, [])

  async function loadProfile() {
    try {
      setIsLoading(true)
      setLoadError(null)

      const [currentProfile, currentCycleSettings, currentCycleHistory, currentCycleGuidance] =
        await Promise.all([
          fetchProfile(),
          fetchCycleSettings().catch(() => null),
          fetchCycleHistory().catch(() => []),
          fetchCycleGuidance().catch(() => null),
        ])

      setProfile(currentProfile)
      setProfileForm(mapProfileToForm(currentProfile))
      setCurrentUser(currentProfile)
      setCycleSettingsForm(currentCycleSettings ? mapCycleSettingsToForm(currentCycleSettings) : emptyCycleSettingsForm)
      setCycleHistory(currentCycleHistory)
      setCycleGuidance(currentCycleGuidance)
    } catch (error) {
      setLoadError(getRequestErrorMessage(error, 'Unable to load your profile.'))
    } finally {
      setIsLoading(false)
    }
  }

  async function refreshCycleData() {
    const [currentCycleSettings, currentCycleHistory, currentCycleGuidance] = await Promise.all([
      fetchCycleSettings(),
      fetchCycleHistory(),
      fetchCycleGuidance(),
    ])

    setCycleSettingsForm(mapCycleSettingsToForm(currentCycleSettings))
    setCycleHistory(currentCycleHistory)
    setCycleGuidance(currentCycleGuidance)
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

  async function handleCycleSettingsSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const validationMessage = validateCycleSettingsForm(cycleSettingsForm)
    if (validationMessage) {
      setCycleError(validationMessage)
      setCycleMessage(null)
      return
    }

    try {
      setIsSavingCycleSettings(true)
      setCycleError(null)
      setCycleMessage(null)

      await updateCycleSettings({
        isEnabled: cycleSettingsForm.isEnabled,
        lastPeriodStartDate: toOptionalValue(cycleSettingsForm.lastPeriodStartDate),
        averageCycleLengthDays: toOptionalNumber(cycleSettingsForm.averageCycleLengthDays),
        averagePeriodLengthDays: toOptionalNumber(cycleSettingsForm.averagePeriodLengthDays),
        cycleRegularity: cycleSettingsForm.cycleRegularity,
        usesHormonalContraception: mapTriStateToBoolean(cycleSettingsForm.usesHormonalContraception),
        isNaturallyCycling: mapTriStateToBoolean(cycleSettingsForm.isNaturallyCycling),
      })

      await refreshCycleData()
      setCycleMessage('Cycle-aware settings updated.')
    } catch (error) {
      setCycleError(getRequestErrorMessage(error, 'Unable to update cycle-aware settings.'))
    } finally {
      setIsSavingCycleSettings(false)
    }
  }

  async function handleCycleEntrySubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const validationMessage = validateCycleEntryForm(cycleEntryForm)
    if (validationMessage) {
      setCycleHistoryError(validationMessage)
      setCycleHistoryMessage(null)
      return
    }

    try {
      setIsSavingCycleEntry(true)
      setCycleHistoryError(null)
      setCycleHistoryMessage(null)

      const payload = {
        periodStartDate: cycleEntryForm.periodStartDate,
        periodEndDate: cycleEntryForm.periodEndDate,
        notes: toOptionalValue(cycleEntryForm.notes),
      }

      if (editingCycleEntryId === null) {
        await createCycleEntry(payload)
        setCycleHistoryMessage('Cycle history entry added.')
      } else {
        await updateCycleEntry(editingCycleEntryId, payload)
        setCycleHistoryMessage('Cycle history entry updated.')
      }

      setCycleEntryForm(emptyCycleEntryForm)
      setEditingCycleEntryId(null)
      await refreshCycleData()
    } catch (error) {
      setCycleHistoryError(getRequestErrorMessage(error, 'Unable to save this cycle history entry.'))
    } finally {
      setIsSavingCycleEntry(false)
    }
  }

  async function handleDeleteCycleEntry(entryId: number) {
    try {
      setDeletingCycleEntryId(entryId)
      setCycleHistoryError(null)
      setCycleHistoryMessage(null)

      await deleteCycleEntry(entryId)
      if (editingCycleEntryId === entryId) {
        setEditingCycleEntryId(null)
        setCycleEntryForm(emptyCycleEntryForm)
      }

      await refreshCycleData()
      setCycleHistoryMessage('Cycle history entry removed.')
    } catch (error) {
      setCycleHistoryError(getRequestErrorMessage(error, 'Unable to delete this cycle history entry.'))
    } finally {
      setDeletingCycleEntryId(null)
    }
  }

  function startEditingCycleEntry(entry: CycleEntry) {
    setEditingCycleEntryId(entry.id)
    setCycleEntryForm({
      periodStartDate: entry.periodStartDate,
      periodEndDate: entry.periodEndDate,
      notes: entry.notes ?? '',
    })
    setCycleHistoryError(null)
    setCycleHistoryMessage(null)
  }

  return (
    <main className="page-shell">
      <section className="hero-panel">
        <div className="hero-copy">
          <span className="eyebrow">Account</span>
          <h1>Profile</h1>
          <p className="hero-text">
            Keep your personal details current, manage your password, and optionally add cycle-aware training guidance based on your own history.
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
          <article className="stat-card">
            <span className="stat-label">Cycle Guidance</span>
            <strong>{cycleGuidance?.isEnabled ? cycleGuidance.estimatedCurrentPhase ?? 'Enabled' : 'Off'}</strong>
            <span className="stat-subtext">
              {cycleGuidance?.isEnabled
                ? cycleGuidance.guidanceHeadline
                : 'Optional training context that stays off until you enable it.'}
            </span>
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
                <p>Update the personal information tied to your account. Sign-in email stays read-only.</p>
              </div>
            </div>

            <form className="weight-form profile-form-panel" onSubmit={handleProfileSubmit}>
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
                    onChange={(event) =>
                      setProfileForm((current) => ({ ...current, fullName: event.target.value }))
                    }
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

            <form className="weight-form profile-form-panel" onSubmit={handlePasswordSubmit}>
              <p className="section-note">
                Use a password you do not reuse elsewhere and confirm it before saving.
              </p>

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
                <small>Enter your current password before choosing a new one.</small>
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
                <small>Choose at least 8 characters.</small>
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

            {passwordMessage || passwordError ? (
              <div className="feedback-stack">
                {passwordMessage ? <p className="feedback success">{passwordMessage}</p> : null}
                {passwordError ? <p className="feedback error">{passwordError}</p> : null}
              </div>
            ) : null}
          </div>

          <div className="panel panel-span-2">
            <div className="panel-header">
              <div>
                <h2>Cycle-Aware Training</h2>
                <p>Optional guidance based on estimated cycle phase, recent training load, and the history you choose to log.</p>
              </div>
            </div>

            <div className="goals-grid cycle-grid">
              <form className="weight-form profile-form-panel" onSubmit={handleCycleSettingsSubmit}>
                <div className="toggle-row">
                  <div>
                    <strong>Enable cycle-aware training guidance</strong>
                    <p className="muted-note">
                      Keep this off unless you want prediction and recovery-aware training suggestions.
                    </p>
                  </div>
                  <label className="toggle-control">
                    <input
                      type="checkbox"
                      checked={cycleSettingsForm.isEnabled}
                      onChange={(event) =>
                        setCycleSettingsForm((current) => ({ ...current, isEnabled: event.target.checked }))
                      }
                    />
                    <span>{cycleSettingsForm.isEnabled ? 'On' : 'Off'}</span>
                  </label>
                </div>

                <div className="form-grid">
                  <label className="field">
                    <span>Last period start date</span>
                    <input
                      type="date"
                      value={cycleSettingsForm.lastPeriodStartDate}
                      max={new Date().toISOString().slice(0, 10)}
                      onChange={(event) =>
                        setCycleSettingsForm((current) => ({
                          ...current,
                          lastPeriodStartDate: event.target.value,
                        }))
                      }
                    />
                  </label>

                  <label className="field">
                    <span>Average cycle length (days)</span>
                    <input
                      type="number"
                      min="20"
                      max="45"
                      step="1"
                      value={cycleSettingsForm.averageCycleLengthDays}
                      onChange={(event) =>
                        setCycleSettingsForm((current) => ({
                          ...current,
                          averageCycleLengthDays: event.target.value,
                        }))
                      }
                      placeholder="e.g. 28"
                    />
                  </label>

                  <label className="field">
                    <span>Average period length (days)</span>
                    <input
                      type="number"
                      min="2"
                      max="10"
                      step="1"
                      value={cycleSettingsForm.averagePeriodLengthDays}
                      onChange={(event) =>
                        setCycleSettingsForm((current) => ({
                          ...current,
                          averagePeriodLengthDays: event.target.value,
                        }))
                      }
                      placeholder="e.g. 5"
                    />
                  </label>

                  <label className="field">
                    <span>Cycle regularity</span>
                    <select
                      className="select-input"
                      value={cycleSettingsForm.cycleRegularity}
                      onChange={(event) =>
                        setCycleSettingsForm((current) => ({
                          ...current,
                          cycleRegularity: event.target.value as CycleRegularity,
                        }))
                      }
                    >
                      <option value="regular">Regular</option>
                      <option value="somewhat-irregular">Somewhat irregular</option>
                      <option value="irregular">Irregular</option>
                    </select>
                  </label>

                  <label className="field">
                    <span>Hormonal contraception</span>
                    <select
                      className="select-input"
                      value={cycleSettingsForm.usesHormonalContraception}
                      onChange={(event) =>
                        setCycleSettingsForm((current) => ({
                          ...current,
                          usesHormonalContraception: event.target.value as CycleSettingsFormState['usesHormonalContraception'],
                        }))
                      }
                    >
                      <option value="unknown">Prefer not to say</option>
                      <option value="yes">Yes</option>
                      <option value="no">No</option>
                    </select>
                  </label>

                  <label className="field">
                    <span>Naturally cycling</span>
                    <select
                      className="select-input"
                      value={cycleSettingsForm.isNaturallyCycling}
                      onChange={(event) =>
                        setCycleSettingsForm((current) => ({
                          ...current,
                          isNaturallyCycling: event.target.value as CycleSettingsFormState['isNaturallyCycling'],
                        }))
                      }
                    >
                      <option value="unknown">Prefer not to say</option>
                      <option value="yes">Yes</option>
                      <option value="no">No</option>
                    </select>
                  </label>
                </div>

                <div className="action-row">
                  <button type="submit" className="primary-button" disabled={isSavingCycleSettings}>
                    {isSavingCycleSettings ? 'Saving guidance settings...' : 'Save cycle settings'}
                  </button>
                </div>

                {cycleMessage || cycleError ? (
                  <div className="feedback-stack">
                    {cycleMessage ? <p className="feedback success">{cycleMessage}</p> : null}
                    {cycleError ? <p className="feedback error">{cycleError}</p> : null}
                  </div>
                ) : null}
              </form>

              <div className="goal-progress-list">
                <article className="assistant-card assistant-card-highlight cycle-guidance-card">
                  <span className="stat-label">Current guidance</span>
                  <strong>{cycleGuidance?.guidanceHeadline ?? 'Cycle-aware guidance is off'}</strong>
                  <p>
                    {cycleGuidance?.guidanceMessage ??
                      'Enable cycle-aware training guidance and add a recent period start date to begin getting practical session context.'}
                  </p>
                  <div className="assistant-list">
                    <div className="assistant-list-item">
                      <strong>Estimated phase</strong>
                      <span>{cycleGuidance?.estimatedCurrentPhase ?? 'Not enough data yet'}</span>
                    </div>
                    <div className="assistant-list-item">
                      <strong>Next period estimate</strong>
                      <span>
                        {cycleGuidance?.estimatedNextPeriodStartDate
                          ? formatDate(cycleGuidance.estimatedNextPeriodStartDate)
                          : 'Not enough data yet'}
                      </span>
                    </div>
                    <div className="assistant-list-item">
                      <strong>Prediction confidence</strong>
                      <span>{cycleGuidance?.predictionConfidence ?? 'Needs data'}</span>
                    </div>
                    <div className="assistant-list-item">
                      <strong>Recent load</strong>
                      <span>
                        {cycleGuidance
                          ? `${cycleGuidance.recentLoadLabel} load from ${cycleGuidance.recentWorkoutCount} workout${cycleGuidance.recentWorkoutCount === 1 ? '' : 's'} in the last 7 days`
                          : 'No guidance yet'}
                      </span>
                    </div>
                  </div>
                </article>
              </div>
            </div>

            <div className="cycle-history-section">
              <div className="section-title-row">
                <div>
                  <h3>Period history</h3>
                  <p>Log past period windows so future estimates can lean on your actual history instead of one-off inputs.</p>
                </div>
              </div>

              <div className="content-grid cycle-history-grid">
                <form className="weight-form profile-form-panel" onSubmit={handleCycleEntrySubmit}>
                  <div className="form-grid">
                    <label className="field">
                      <span>Period start date</span>
                      <input
                        type="date"
                        value={cycleEntryForm.periodStartDate}
                        max={new Date().toISOString().slice(0, 10)}
                        onChange={(event) =>
                          setCycleEntryForm((current) => ({
                            ...current,
                            periodStartDate: event.target.value,
                          }))
                        }
                        required
                      />
                    </label>

                    <label className="field">
                      <span>Period end date</span>
                      <input
                        type="date"
                        value={cycleEntryForm.periodEndDate}
                        max={new Date().toISOString().slice(0, 10)}
                        onChange={(event) =>
                          setCycleEntryForm((current) => ({
                            ...current,
                            periodEndDate: event.target.value,
                          }))
                        }
                        required
                      />
                    </label>

                    <label className="field field-span-2">
                      <span>Notes</span>
                      <textarea
                        className="text-area"
                        rows={3}
                        maxLength={500}
                        value={cycleEntryForm.notes}
                        onChange={(event) =>
                          setCycleEntryForm((current) => ({ ...current, notes: event.target.value }))
                        }
                        placeholder="Optional context like unusually hard training or recovery notes."
                      />
                    </label>
                  </div>

                  <div className="action-row">
                    {editingCycleEntryId !== null ? (
                      <button
                        type="button"
                        className="ghost-button"
                        onClick={() => {
                          setEditingCycleEntryId(null)
                          setCycleEntryForm(emptyCycleEntryForm)
                        }}
                      >
                        Cancel edit
                      </button>
                    ) : null}
                    <button type="submit" className="primary-button" disabled={isSavingCycleEntry}>
                      {isSavingCycleEntry
                        ? 'Saving history...'
                        : editingCycleEntryId === null
                          ? 'Add history entry'
                          : 'Update history entry'}
                    </button>
                  </div>

                  {cycleHistoryMessage || cycleHistoryError ? (
                    <div className="feedback-stack">
                      {cycleHistoryMessage ? <p className="feedback success">{cycleHistoryMessage}</p> : null}
                      {cycleHistoryError ? <p className="feedback error">{cycleHistoryError}</p> : null}
                    </div>
                  ) : null}
                </form>

                <div className="entry-list">
                  {cycleHistory.length === 0 ? (
                    <StateCard
                      title="No cycle history yet"
                      description="Add your past period dates here to improve phase and next-cycle estimates over time."
                    />
                  ) : (
                    cycleHistory.map((entry) => (
                      <article key={entry.id} className="entry-card">
                        <div className="entry-copy">
                          <p className="entry-date">
                            {formatDate(entry.periodStartDate)} to {formatDate(entry.periodEndDate)}
                          </p>
                          <strong className="entry-weight">
                            {getDaySpan(entry.periodStartDate, entry.periodEndDate)} day
                            {getDaySpan(entry.periodStartDate, entry.periodEndDate) === 1 ? '' : 's'}
                          </strong>
                          <span className="record-hint">Logged {formatDate(entry.createdAt)}</span>
                          {entry.notes ? <p className="workout-notes">{entry.notes}</p> : null}
                        </div>

                        <div className="entry-actions">
                          <button
                            type="button"
                            className="ghost-button"
                            onClick={() => startEditingCycleEntry(entry)}
                          >
                            Edit
                          </button>
                          <button
                            type="button"
                            className="ghost-button subtle-danger-button"
                            onClick={() => void handleDeleteCycleEntry(entry.id)}
                            disabled={deletingCycleEntryId === entry.id}
                          >
                            {deletingCycleEntryId === entry.id ? 'Removing...' : 'Delete'}
                          </button>
                        </div>
                      </article>
                    ))
                  )}
                </div>
              </div>
            </div>
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

function mapCycleSettingsToForm(settings: CycleSettings): CycleSettingsFormState {
  return {
    isEnabled: settings.isEnabled,
    lastPeriodStartDate: settings.lastPeriodStartDate ?? '',
    averageCycleLengthDays:
      settings.averageCycleLengthDays === null ? '' : settings.averageCycleLengthDays.toString(),
    averagePeriodLengthDays:
      settings.averagePeriodLengthDays === null ? '' : settings.averagePeriodLengthDays.toString(),
    cycleRegularity: settings.cycleRegularity,
    usesHormonalContraception: mapBooleanToTriState(settings.usesHormonalContraception),
    isNaturallyCycling: mapBooleanToTriState(settings.isNaturallyCycling),
  }
}

function toOptionalValue(value: string) {
  const trimmedValue = value.trim()
  return trimmedValue === '' ? null : trimmedValue
}

function toOptionalNumber(value: string) {
  const trimmedValue = value.trim()
  return trimmedValue === '' ? null : Number(trimmedValue)
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

function validateCycleSettingsForm(form: CycleSettingsFormState) {
  if (!form.isEnabled) {
    return null
  }

  if (!form.lastPeriodStartDate) {
    return 'Last period start date is required when cycle-aware guidance is enabled.'
  }

  const cycleLength = Number(form.averageCycleLengthDays)
  if (!form.averageCycleLengthDays || Number.isNaN(cycleLength) || cycleLength < 20 || cycleLength > 45) {
    return 'Average cycle length must be between 20 and 45 days.'
  }

  const periodLength = Number(form.averagePeriodLengthDays)
  if (!form.averagePeriodLengthDays || Number.isNaN(periodLength) || periodLength < 2 || periodLength > 10) {
    return 'Average period length must be between 2 and 10 days.'
  }

  if (periodLength >= cycleLength) {
    return 'Average period length must be shorter than average cycle length.'
  }

  return null
}

function validateCycleEntryForm(form: CycleEntryFormState) {
  if (!form.periodStartDate || !form.periodEndDate) {
    return 'Period start and end dates are required.'
  }

  if (form.periodEndDate < form.periodStartDate) {
    return 'Period end date must be on or after the start date.'
  }

  const today = new Date().toISOString().slice(0, 10)
  if (form.periodStartDate > today || form.periodEndDate > today) {
    return 'Cycle history cannot be saved in the future.'
  }

  return null
}

function mapBooleanToTriState(value: boolean | null) {
  if (value === true) {
    return 'yes'
  }

  if (value === false) {
    return 'no'
  }

  return 'unknown'
}

function mapTriStateToBoolean(value: 'yes' | 'no' | 'unknown') {
  if (value === 'yes') {
    return true
  }

  if (value === 'no') {
    return false
  }

  return null
}

function getDaySpan(startDate: string, endDate: string) {
  const start = new Date(startDate)
  const end = new Date(endDate)
  return Math.max(1, Math.round((end.getTime() - start.getTime()) / 86400000) + 1)
}
