import { useEffect, useState, type FormEvent } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import {
  createCycleEntry,
  createCycleSymptomLog,
  deleteCycleEntry,
  deleteCycleSymptomLog,
  fetchCycleGuidance,
  fetchCycleHistory,
  fetchCycleSettings,
  fetchCycleSymptomLogs,
  updateCycleEntry,
  updateCycleSettings,
  updateCycleSymptomLog,
} from '../api/cycle'
import { StateCard } from '../components/StateCard'
import { formatDate } from '../lib/format'
import { getRequestErrorMessage } from '../lib/http'
import type {
  CycleEntry,
  CycleGuidance,
  CycleRegularity,
  CycleSettings,
  CycleSymptomLog,
} from '../types/cycle'

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

type SymptomFormState = {
  date: string
  fatigueLevel: string
  crampsLevel: string
  mood: string
  bloatingLevel: string
  sleepQuality: string
  recoveryFeeling: string
  notes: string
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

const emptyCycleEntryForm: CycleEntryFormState = {
  periodStartDate: '',
  periodEndDate: '',
  notes: '',
}

const emptySymptomForm: SymptomFormState = {
  date: new Date().toISOString().slice(0, 10),
  fatigueLevel: '3',
  crampsLevel: '1',
  mood: 'steady',
  bloatingLevel: '1',
  sleepQuality: '3',
  recoveryFeeling: '3',
  notes: '',
}

export function CyclePage() {
  const navigate = useNavigate()
  const [cycleSettings, setCycleSettings] = useState<CycleSettings | null>(null)
  const [cycleSettingsForm, setCycleSettingsForm] = useState<CycleSettingsFormState>(emptyCycleSettingsForm)
  const [cycleGuidance, setCycleGuidance] = useState<CycleGuidance | null>(null)
  const [cycleHistory, setCycleHistory] = useState<CycleEntry[]>([])
  const [cycleSymptoms, setCycleSymptoms] = useState<CycleSymptomLog[]>([])
  const [cycleEntryForm, setCycleEntryForm] = useState<CycleEntryFormState>(emptyCycleEntryForm)
  const [symptomForm, setSymptomForm] = useState<SymptomFormState>(emptySymptomForm)
  const [editingCycleEntryId, setEditingCycleEntryId] = useState<number | null>(null)
  const [editingSymptomLogId, setEditingSymptomLogId] = useState<number | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isSavingCycleSettings, setIsSavingCycleSettings] = useState(false)
  const [isSavingCycleEntry, setIsSavingCycleEntry] = useState(false)
  const [isSavingSymptomLog, setIsSavingSymptomLog] = useState(false)
  const [deletingCycleEntryId, setDeletingCycleEntryId] = useState<number | null>(null)
  const [deletingSymptomLogId, setDeletingSymptomLogId] = useState<number | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [cycleError, setCycleError] = useState<string | null>(null)
  const [cycleMessage, setCycleMessage] = useState<string | null>(null)
  const [cycleHistoryError, setCycleHistoryError] = useState<string | null>(null)
  const [cycleHistoryMessage, setCycleHistoryMessage] = useState<string | null>(null)
  const [symptomError, setSymptomError] = useState<string | null>(null)
  const [symptomMessage, setSymptomMessage] = useState<string | null>(null)

  useEffect(() => {
    void loadCyclePage()
  }, [])

  async function loadCyclePage() {
    try {
      setIsLoading(true)
      setLoadError(null)

      const [settings, guidance, history, symptoms] = await Promise.all([
        fetchCycleSettings(),
        fetchCycleGuidance().catch(() => null),
        fetchCycleHistory(),
        fetchCycleSymptomLogs(),
      ])

      setCycleSettings(settings)
      setCycleSettingsForm(mapCycleSettingsToForm(settings))
      setCycleGuidance(guidance)
      setCycleHistory(history)
      setCycleSymptoms(symptoms)
    } catch (error) {
      setLoadError(getRequestErrorMessage(error, 'Unable to load cycle guidance.'))
    } finally {
      setIsLoading(false)
    }
  }

  async function refreshCyclePage() {
    const [settings, guidance, history, symptoms] = await Promise.all([
      fetchCycleSettings(),
      fetchCycleGuidance(),
      fetchCycleHistory(),
      fetchCycleSymptomLogs(),
    ])

    setCycleSettings(settings)
    setCycleSettingsForm(mapCycleSettingsToForm(settings))
    setCycleGuidance(guidance)
    setCycleHistory(history)
    setCycleSymptoms(symptoms)
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

      const savedSettings = await updateCycleSettings({
        isEnabled: cycleSettingsForm.isEnabled,
        lastPeriodStartDate: toOptionalValue(cycleSettingsForm.lastPeriodStartDate),
        averageCycleLengthDays: toOptionalNumber(cycleSettingsForm.averageCycleLengthDays),
        averagePeriodLengthDays: toOptionalNumber(cycleSettingsForm.averagePeriodLengthDays),
        cycleRegularity: cycleSettingsForm.cycleRegularity,
        usesHormonalContraception: mapTriStateToBoolean(cycleSettingsForm.usesHormonalContraception),
        isNaturallyCycling: mapTriStateToBoolean(cycleSettingsForm.isNaturallyCycling),
      })

      window.dispatchEvent(new CustomEvent('cycle-settings-updated', { detail: { isEnabled: savedSettings.isEnabled } }))

      if (!savedSettings.isEnabled) {
        navigate('/profile', { replace: true })
        return
      }

      await refreshCyclePage()
      setCycleMessage('Cycle settings updated.')
    } catch (error) {
      setCycleError(getRequestErrorMessage(error, 'Unable to update cycle settings.'))
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
        setCycleHistoryMessage('Period history entry added.')
      } else {
        await updateCycleEntry(editingCycleEntryId, payload)
        setCycleHistoryMessage('Period history entry updated.')
      }

      setCycleEntryForm(emptyCycleEntryForm)
      setEditingCycleEntryId(null)
      await refreshCyclePage()
    } catch (error) {
      setCycleHistoryError(getRequestErrorMessage(error, 'Unable to save this period history entry.'))
    } finally {
      setIsSavingCycleEntry(false)
    }
  }

  async function handleSymptomSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const validationMessage = validateSymptomForm(symptomForm)
    if (validationMessage) {
      setSymptomError(validationMessage)
      setSymptomMessage(null)
      return
    }

    try {
      setIsSavingSymptomLog(true)
      setSymptomError(null)
      setSymptomMessage(null)

      const payload = {
        date: symptomForm.date,
        fatigueLevel: Number(symptomForm.fatigueLevel),
        crampsLevel: Number(symptomForm.crampsLevel),
        mood: symptomForm.mood.trim(),
        bloatingLevel: Number(symptomForm.bloatingLevel),
        sleepQuality: Number(symptomForm.sleepQuality),
        recoveryFeeling: Number(symptomForm.recoveryFeeling),
        notes: toOptionalValue(symptomForm.notes),
      }

      if (editingSymptomLogId === null) {
        await createCycleSymptomLog(payload)
        setSymptomMessage('Symptom and recovery log added.')
      } else {
        await updateCycleSymptomLog(editingSymptomLogId, payload)
        setSymptomMessage('Symptom and recovery log updated.')
      }

      setEditingSymptomLogId(null)
      setSymptomForm(emptySymptomForm)
      await refreshCyclePage()
    } catch (error) {
      setSymptomError(getRequestErrorMessage(error, 'Unable to save this symptom log.'))
    } finally {
      setIsSavingSymptomLog(false)
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
      await refreshCyclePage()
      setCycleHistoryMessage('Period history entry removed.')
    } catch (error) {
      setCycleHistoryError(getRequestErrorMessage(error, 'Unable to delete this period history entry.'))
    } finally {
      setDeletingCycleEntryId(null)
    }
  }

  async function handleDeleteSymptomLog(logId: number) {
    try {
      setDeletingSymptomLogId(logId)
      setSymptomError(null)
      setSymptomMessage(null)
      await deleteCycleSymptomLog(logId)
      if (editingSymptomLogId === logId) {
        setEditingSymptomLogId(null)
        setSymptomForm(emptySymptomForm)
      }
      await refreshCyclePage()
      setSymptomMessage('Symptom log removed.')
    } catch (error) {
      setSymptomError(getRequestErrorMessage(error, 'Unable to delete this symptom log.'))
    } finally {
      setDeletingSymptomLogId(null)
    }
  }

  if (isLoading) {
    return (
      <main className="page-shell">
        <section className="content-grid">
          <div className="panel panel-span-2">
            <StateCard title="Loading cycle guidance" description="Collecting settings, history, and recovery data." loading />
          </div>
        </section>
      </main>
    )
  }

  if (loadError) {
    return (
      <main className="page-shell">
        <section className="content-grid">
          <div className="panel panel-span-2">
            <StateCard title="Cycle guidance unavailable" description={loadError} tone="error" />
          </div>
        </section>
      </main>
    )
  }

  if (!cycleSettings?.isEnabled) {
    return <Navigate to="/profile" replace />
  }

  return (
    <main className="page-shell">
      <section className="hero-panel">
        <div className="hero-copy">
          <span className="eyebrow">Cycle</span>
          <h1>Cycle-Aware Training</h1>
          <p className="hero-text">
            Keep your cycle settings, period history, and symptom logs in one place so training guidance can reflect both estimated phase and how you are actually feeling.
          </p>
        </div>

        <div className="stats-grid">
          <article className="stat-card stat-card-emphasis">
            <span className="stat-label">Current Phase</span>
            <strong>
              {cycleSettings.canPredict
                ? cycleGuidance?.estimatedCurrentPhase ?? 'Building estimate'
                : 'Setup incomplete'}
            </strong>
            <span className="stat-subtext">
              {cycleSettings.canPredict && cycleGuidance?.currentCycleDay
                ? `Cycle day ${cycleGuidance.currentCycleDay}`
                : cycleSettings.setupMessage ?? 'Add enough data to estimate your current phase'}
            </span>
          </article>
          <article className="stat-card">
            <span className="stat-label">Next Period</span>
            <strong>
              {cycleGuidance?.estimatedNextPeriodStartDate
                ? formatDate(cycleGuidance.estimatedNextPeriodStartDate)
                : 'Not enough data'}
            </strong>
            <span className="stat-subtext">Estimated from your history and current settings</span>
          </article>
          <article className="stat-card">
            <span className="stat-label">Prediction Confidence</span>
            <strong>{cycleGuidance?.predictionConfidence ?? 'Needs data'}</strong>
            <span className="stat-subtext">More history usually improves estimates</span>
          </article>
          <article className="stat-card">
            <span className="stat-label">Recent Symptom Load</span>
            <strong>{cycleGuidance?.symptomLoadLabel ?? 'Unknown'}</strong>
            <span className="stat-subtext">
              {cycleGuidance?.latestSymptomLogDate
                ? `Latest log ${formatDate(cycleGuidance.latestSymptomLogDate)}`
                : 'No symptom log yet'}
            </span>
          </article>
        </div>
      </section>

      <section className="content-grid">
        <div className="panel panel-span-2">
          <div className="panel-header">
            <div>
              <h2>Guidance Summary</h2>
              <p>Practical cycle-aware training context based on estimated phase, recent training load, and your symptom logs.</p>
            </div>
          </div>

          <div className="assistant-grid cycle-guidance-overview-grid">
            <article className="assistant-card assistant-card-highlight">
              <span className="stat-label">Current recommendation</span>
              <strong>{cycleGuidance?.guidanceHeadline ?? 'No guidance yet'}</strong>
              <p>
                {cycleSettings.setupMessage ??
                  cycleGuidance?.guidanceMessage ??
                  'Add more cycle data to unlock a more useful estimate.'}
              </p>
            </article>

            <article className="assistant-card">
              <span className="stat-label">Recent load</span>
              <strong>{cycleGuidance?.recentLoadLabel ?? 'Unknown'}</strong>
              <p>
                {cycleGuidance
                  ? `${cycleGuidance.recentWorkoutCount} workouts, ${cycleGuidance.recentSetCount} sets, and ${cycleGuidance.recentTrainingLoad} kg of recent reps x load in the last 7 days.`
                  : 'No recent load summary yet.'}
              </p>
            </article>

            <article className="assistant-card">
              <span className="stat-label">Recent recovery signals</span>
              <strong>{cycleGuidance?.symptomLoadLabel ?? 'Unknown'}</strong>
              <p>
                {cycleGuidance?.latestSymptomLogDate
                  ? `Fatigue ${cycleGuidance.recentFatigueLevel ?? '-'}, cramps ${cycleGuidance.recentCrampsLevel ?? '-'}, sleep ${cycleGuidance.recentSleepQuality ?? '-'}, recovery ${cycleGuidance.recentRecoveryFeeling ?? '-'} from your latest log.`
                  : 'Log symptoms and recovery to make the guidance more personal.'}
              </p>
            </article>
          </div>

          {cycleGuidance?.insights.length ? (
            <div className="assistant-list cycle-insight-list">
              {cycleGuidance.insights.map((insight, index) => (
                <div key={`${index}-${insight}`} className="assistant-list-item">
                  <strong>Insight {index + 1}</strong>
                  <span>{insight}</span>
                </div>
              ))}
            </div>
          ) : null}
        </div>
      </section>

      <section className="content-grid cycle-page-grid">
        <div className="panel">
          <div className="panel-header">
            <div>
              <h2>Cycle Settings</h2>
              <p>Adjust the inputs used for prediction quality and phase estimation.</p>
            </div>
          </div>

          <form className="weight-form profile-form-panel" onSubmit={handleCycleSettingsSubmit}>
            <div className="toggle-row">
              <div>
                <strong>Cycle-aware guidance is enabled</strong>
                <p className="muted-note">Turning this off hides the Cycle page and removes cycle-specific guidance elsewhere in the app.</p>
              </div>
              <label className="toggle-control">
                <input
                  type="checkbox"
                  checked={cycleSettingsForm.isEnabled}
                  onChange={(event) => setCycleSettingsForm((current) => ({ ...current, isEnabled: event.target.checked }))}
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
                  onChange={(event) => setCycleSettingsForm((current) => ({ ...current, lastPeriodStartDate: event.target.value }))}
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
                  onChange={(event) => setCycleSettingsForm((current) => ({ ...current, averageCycleLengthDays: event.target.value }))}
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
                  onChange={(event) => setCycleSettingsForm((current) => ({ ...current, averagePeriodLengthDays: event.target.value }))}
                />
              </label>

              <label className="field">
                <span>Cycle regularity</span>
                <select
                  className="select-input"
                  value={cycleSettingsForm.cycleRegularity}
                  onChange={(event) => setCycleSettingsForm((current) => ({ ...current, cycleRegularity: event.target.value as CycleRegularity }))}
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
                  onChange={(event) => setCycleSettingsForm((current) => ({ ...current, usesHormonalContraception: event.target.value as CycleSettingsFormState['usesHormonalContraception'] }))}
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
                  onChange={(event) => setCycleSettingsForm((current) => ({ ...current, isNaturallyCycling: event.target.value as CycleSettingsFormState['isNaturallyCycling'] }))}
                >
                  <option value="unknown">Prefer not to say</option>
                  <option value="yes">Yes</option>
                  <option value="no">No</option>
                </select>
              </label>
            </div>

            <div className="action-row">
              <button type="submit" className="primary-button" disabled={isSavingCycleSettings}>
                {isSavingCycleSettings ? 'Saving settings...' : 'Save cycle settings'}
              </button>
            </div>

            {cycleMessage || cycleError ? (
              <div className="feedback-stack">
                {cycleMessage ? <p className="feedback success">{cycleMessage}</p> : null}
                {cycleError ? <p className="feedback error">{cycleError}</p> : null}
              </div>
            ) : null}
          </form>
        </div>

        <div className="panel">
          <div className="panel-header">
            <div>
              <h2>Period History</h2>
              <p>Track start and end dates so prediction quality can improve over time.</p>
            </div>
          </div>

          <form className="weight-form profile-form-panel" onSubmit={handleCycleEntrySubmit}>
            <div className="form-grid">
              <label className="field">
                <span>Period start date</span>
                <input
                  type="date"
                  value={cycleEntryForm.periodStartDate}
                  max={new Date().toISOString().slice(0, 10)}
                  onChange={(event) => setCycleEntryForm((current) => ({ ...current, periodStartDate: event.target.value }))}
                />
              </label>

              <label className="field">
                <span>Period end date</span>
                <input
                  type="date"
                  value={cycleEntryForm.periodEndDate}
                  max={new Date().toISOString().slice(0, 10)}
                  onChange={(event) => setCycleEntryForm((current) => ({ ...current, periodEndDate: event.target.value }))}
                />
              </label>

              <label className="field field-span-2">
                <span>Notes</span>
                <textarea
                  className="text-area"
                  rows={3}
                  maxLength={500}
                  value={cycleEntryForm.notes}
                  onChange={(event) => setCycleEntryForm((current) => ({ ...current, notes: event.target.value }))}
                  placeholder="Optional context for this cycle window."
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
              <StateCard title="No period history yet" description="Add your past period windows to improve future estimates." />
            ) : (
              cycleHistory.map((entry) => (
                <article key={entry.id} className="entry-card">
                  <div className="entry-copy">
                    <p className="entry-date">{formatDate(entry.periodStartDate)} to {formatDate(entry.periodEndDate)}</p>
                    <strong className="entry-weight">{getDaySpan(entry.periodStartDate, entry.periodEndDate)} days</strong>
                    {entry.notes ? <p className="workout-notes">{entry.notes}</p> : null}
                  </div>

                  <div className="entry-actions">
                    <button
                      type="button"
                      className="ghost-button"
                      onClick={() => {
                        setEditingCycleEntryId(entry.id)
                        setCycleEntryForm({
                          periodStartDate: entry.periodStartDate,
                          periodEndDate: entry.periodEndDate,
                          notes: entry.notes ?? '',
                        })
                      }}
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
      </section>

      <section className="content-grid cycle-page-grid">
        <div className="panel">
          <div className="panel-header">
            <div>
              <h2>Symptoms And Recovery</h2>
              <p>Optional logs that help the guidance reflect how you are actually feeling instead of phase alone.</p>
            </div>
          </div>

          <form className="weight-form profile-form-panel" onSubmit={handleSymptomSubmit}>
            <div className="form-grid">
              <label className="field">
                <span>Date</span>
                <input
                  type="date"
                  value={symptomForm.date}
                  max={new Date().toISOString().slice(0, 10)}
                  onChange={(event) => setSymptomForm((current) => ({ ...current, date: event.target.value }))}
                />
              </label>

              <label className="field">
                <span>Mood</span>
                <input
                  type="text"
                  maxLength={50}
                  value={symptomForm.mood}
                  onChange={(event) => setSymptomForm((current) => ({ ...current, mood: event.target.value }))}
                  placeholder="steady, low, focused..."
                />
              </label>

              <RangeField label="Fatigue level" value={symptomForm.fatigueLevel} onChange={(value) => setSymptomForm((current) => ({ ...current, fatigueLevel: value }))} />
              <RangeField label="Cramps level" value={symptomForm.crampsLevel} onChange={(value) => setSymptomForm((current) => ({ ...current, crampsLevel: value }))} />
              <RangeField label="Bloating level" value={symptomForm.bloatingLevel} onChange={(value) => setSymptomForm((current) => ({ ...current, bloatingLevel: value }))} />
              <RangeField label="Sleep quality" value={symptomForm.sleepQuality} onChange={(value) => setSymptomForm((current) => ({ ...current, sleepQuality: value }))} />
              <RangeField label="Recovery feeling" value={symptomForm.recoveryFeeling} onChange={(value) => setSymptomForm((current) => ({ ...current, recoveryFeeling: value }))} />

              <label className="field field-span-2">
                <span>Notes</span>
                <textarea
                  className="text-area"
                  rows={3}
                  maxLength={500}
                  value={symptomForm.notes}
                  onChange={(event) => setSymptomForm((current) => ({ ...current, notes: event.target.value }))}
                  placeholder="Optional notes about training tolerance, soreness, appetite, or recovery."
                />
              </label>
            </div>

            <div className="action-row">
              {editingSymptomLogId !== null ? (
                <button
                  type="button"
                  className="ghost-button"
                  onClick={() => {
                    setEditingSymptomLogId(null)
                    setSymptomForm(emptySymptomForm)
                  }}
                >
                  Cancel edit
                </button>
              ) : null}
              <button type="submit" className="primary-button" disabled={isSavingSymptomLog}>
                {isSavingSymptomLog
                  ? 'Saving symptom log...'
                  : editingSymptomLogId === null
                    ? 'Add symptom log'
                    : 'Update symptom log'}
              </button>
            </div>

            {symptomMessage || symptomError ? (
              <div className="feedback-stack">
                {symptomMessage ? <p className="feedback success">{symptomMessage}</p> : null}
                {symptomError ? <p className="feedback error">{symptomError}</p> : null}
              </div>
            ) : null}
          </form>
        </div>

        <div className="panel">
          <div className="panel-header">
            <div>
              <h2>Recent Symptom Logs</h2>
              <p>Keep these lightweight. Even short check-ins can make the guidance more personal.</p>
            </div>
          </div>

          <div className="entry-list">
            {cycleSymptoms.length === 0 ? (
              <StateCard title="No symptom logs yet" description="Add a quick fatigue and recovery check-in to make guidance more personal." />
            ) : (
              cycleSymptoms.map((log) => (
                <article key={log.id} className="entry-card">
                  <div className="entry-copy">
                    <p className="entry-date">{formatDate(log.date)}</p>
                    <strong className="entry-weight">{log.mood}</strong>
                    <span className="record-hint">
                      Fatigue {log.fatigueLevel} • Cramps {log.crampsLevel} • Sleep {log.sleepQuality} • Recovery {log.recoveryFeeling}
                    </span>
                    {log.notes ? <p className="workout-notes">{log.notes}</p> : null}
                  </div>

                  <div className="entry-actions">
                    <button
                      type="button"
                      className="ghost-button"
                      onClick={() => {
                        setEditingSymptomLogId(log.id)
                        setSymptomForm({
                          date: log.date,
                          fatigueLevel: log.fatigueLevel.toString(),
                          crampsLevel: log.crampsLevel.toString(),
                          mood: log.mood,
                          bloatingLevel: log.bloatingLevel.toString(),
                          sleepQuality: log.sleepQuality.toString(),
                          recoveryFeeling: log.recoveryFeeling.toString(),
                          notes: log.notes ?? '',
                        })
                      }}
                    >
                      Edit
                    </button>
                    <button
                      type="button"
                      className="ghost-button subtle-danger-button"
                      onClick={() => void handleDeleteSymptomLog(log.id)}
                      disabled={deletingSymptomLogId === log.id}
                    >
                      {deletingSymptomLogId === log.id ? 'Removing...' : 'Delete'}
                    </button>
                  </div>
                </article>
              ))
            )}
          </div>
        </div>
      </section>
    </main>
  )
}

function RangeField({
  label,
  value,
  onChange,
}: {
  label: string
  value: string
  onChange: (value: string) => void
}) {
  return (
    <label className="field">
      <span>{label}</span>
      <select className="select-input" value={value} onChange={(event) => onChange(event.target.value)}>
        <option value="1">1</option>
        <option value="2">2</option>
        <option value="3">3</option>
        <option value="4">4</option>
        <option value="5">5</option>
      </select>
      <small>1 is light and 5 is high.</small>
    </label>
  )
}

function mapCycleSettingsToForm(settings: CycleSettings): CycleSettingsFormState {
  return {
    isEnabled: settings.isEnabled,
    lastPeriodStartDate: settings.lastPeriodStartDate ?? '',
    averageCycleLengthDays: settings.averageCycleLengthDays === null ? '' : settings.averageCycleLengthDays.toString(),
    averagePeriodLengthDays: settings.averagePeriodLengthDays === null ? '' : settings.averagePeriodLengthDays.toString(),
    cycleRegularity: settings.cycleRegularity,
    usesHormonalContraception: mapBooleanToTriState(settings.usesHormonalContraception),
    isNaturallyCycling: mapBooleanToTriState(settings.isNaturallyCycling),
  }
}

function validateCycleSettingsForm(form: CycleSettingsFormState) {
  if (!form.isEnabled) {
    return null
  }

  if (form.averageCycleLengthDays) {
    const cycleLength = Number(form.averageCycleLengthDays)
    if (Number.isNaN(cycleLength) || cycleLength < 20 || cycleLength > 45) {
      return 'Average cycle length must be between 20 and 45 days.'
    }
  }

  if (form.averagePeriodLengthDays) {
    const periodLength = Number(form.averagePeriodLengthDays)
    if (Number.isNaN(periodLength) || periodLength < 2 || periodLength > 10) {
      return 'Average period length must be between 2 and 10 days.'
    }
  }

  if (form.averageCycleLengthDays && form.averagePeriodLengthDays) {
    const cycleLength = Number(form.averageCycleLengthDays)
    const periodLength = Number(form.averagePeriodLengthDays)
    if (periodLength >= cycleLength) {
      return 'Average period length must be shorter than average cycle length.'
    }
  }

  if (form.lastPeriodStartDate && form.lastPeriodStartDate > new Date().toISOString().slice(0, 10)) {
    return 'Last period start date cannot be in the future.'
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
    return 'Period history cannot be saved in the future.'
  }

  return null
}

function validateSymptomForm(form: SymptomFormState) {
  if (!form.date) {
    return 'Date is required.'
  }

  if (form.date > new Date().toISOString().slice(0, 10)) {
    return 'Symptom logs cannot be saved in the future.'
  }

  if (!form.mood.trim()) {
    return 'Mood is required.'
  }

  return null
}

function toOptionalValue(value: string) {
  const trimmedValue = value.trim()
  return trimmedValue === '' ? null : trimmedValue
}

function toOptionalNumber(value: string) {
  const trimmedValue = value.trim()
  return trimmedValue === '' ? null : Number(trimmedValue)
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
