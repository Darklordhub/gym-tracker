import { useDeferredValue, useEffect, useState } from 'react'
import { Copy, KeyRound, Pencil, RefreshCw, Sparkles } from 'lucide-react'
import {
  approveExerciseMediaDraft,
  createExerciseMediaDraft,
  fetchAdminExerciseCatalog,
  fetchAdminUsers,
  generateExerciseMediaDraft,
  getExerciseMediaDraft,
  getExerciseMediaDraftThumbnail,
  getExerciseMediaDrafts,
  getExerciseMediaDraftVideo,
  getExerciseMediaStudioExercise,
  publishExerciseMediaDraft,
  refreshExerciseMediaDraftStatus,
  rejectExerciseMediaDraft,
  resetAdminExerciseCatalogItem,
  resetAdminUserPassword,
  syncAdminExerciseCatalogFromWger,
  updateAdminExerciseCatalogItem,
  updateAdminUserRole,
  updateAdminUserStatus,
} from '../api/admin'
import { StateCard } from '../components/StateCard'
import { useAuth } from '../auth/useAuth'
import { formatDate } from '../lib/format'
import { getRequestErrorMessage, isForbiddenError } from '../lib/http'
import type {
  AdminExerciseCatalogItem,
  AdminUser,
  CreateExerciseMediaDraftRequest,
  ExerciseMediaDraftMediaType,
  ExerciseMediaDraftResponse,
  ExerciseMediaStudioExerciseResponse,
  RejectExerciseMediaDraftRequest,
  ReviewExerciseMediaDraftRequest,
  UpdateExerciseCatalogItemPayload,
} from '../types/admin'

type PendingActionMap = Record<string, boolean>
type DraftMediaPreviewKind = 'video' | 'thumbnail'

type DraftMediaPreview = {
  draftId: number
  kind: DraftMediaPreviewKind
  objectUrl: string
}

const catalogMobilePageSize = 5
const catalogMobileQuery = '(max-width: 640px)'
const mediaDraftTypeOptions: ExerciseMediaDraftMediaType[] = ['Video', 'Thumbnail', 'Image']

const initialCatalogFormState = (): UpdateExerciseCatalogItemPayload => ({
  name: '',
  instructions: '',
  thumbnailUrl: '',
  videoUrl: '',
  isActive: true,
})

export function AdminPage() {
  const { authState } = useAuth()
  const [users, setUsers] = useState<AdminUser[]>([])
  const [catalogItems, setCatalogItems] = useState<AdminExerciseCatalogItem[]>([])
  const [mediaDrafts, setMediaDrafts] = useState<ExerciseMediaDraftResponse[]>([])
  const [isLoadingUsers, setIsLoadingUsers] = useState(true)
  const [isLoadingCatalog, setIsLoadingCatalog] = useState(true)
  const [isLoadingMediaDrafts, setIsLoadingMediaDrafts] = useState(true)
  const [isLoadingStudioExercise, setIsLoadingStudioExercise] = useState(false)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)
  const [mediaStudioError, setMediaStudioError] = useState<string | null>(null)
  const [pendingActions, setPendingActions] = useState<PendingActionMap>({})
  const [resetPasswordUser, setResetPasswordUser] = useState<AdminUser | null>(null)
  const [resetPasswordForm, setResetPasswordForm] = useState({ newPassword: '', confirmPassword: '' })
  const [resetPasswordError, setResetPasswordError] = useState<string | null>(null)
  const [catalogSearch, setCatalogSearch] = useState('')
  const [editingCatalogItem, setEditingCatalogItem] = useState<AdminExerciseCatalogItem | null>(null)
  const [catalogForm, setCatalogForm] = useState<UpdateExerciseCatalogItemPayload>(initialCatalogFormState)
  const [catalogFormError, setCatalogFormError] = useState<string | null>(null)
  const [selectedMediaExerciseId, setSelectedMediaExerciseId] = useState<number | ''>('')
  const [selectedMediaType, setSelectedMediaType] = useState<ExerciseMediaDraftMediaType>('Video')
  const [selectedStudioExercise, setSelectedStudioExercise] = useState<ExerciseMediaStudioExerciseResponse | null>(null)
  const [promptDraft, setPromptDraft] = useState<ExerciseMediaDraftResponse | null>(null)
  const [draftMediaPreview, setDraftMediaPreview] = useState<DraftMediaPreview | null>(null)
  const [mobileCatalogVisibleCount, setMobileCatalogVisibleCount] = useState(catalogMobilePageSize)
  const isMobileCatalogList = useMediaQuery(catalogMobileQuery)
  const deferredCatalogSearch = useDeferredValue(catalogSearch)
  const visibleCatalogItems = isMobileCatalogList
    ? catalogItems.slice(0, mobileCatalogVisibleCount)
    : catalogItems
  const catalogVisibleCount = Math.min(visibleCatalogItems.length, catalogItems.length)
  const canLoadMoreCatalogItems = isMobileCatalogList && catalogVisibleCount < catalogItems.length

  useEffect(() => {
    void loadUsers()
    void loadMediaDrafts()
  }, [])

  useEffect(() => {
    void loadCatalog(deferredCatalogSearch)
  }, [deferredCatalogSearch])

  useEffect(() => {
    setMobileCatalogVisibleCount(catalogMobilePageSize)
  }, [deferredCatalogSearch])

  useEffect(() => {
    if (isMobileCatalogList) {
      setMobileCatalogVisibleCount(catalogMobilePageSize)
    }
  }, [isMobileCatalogList])

  useEffect(() => {
    if (selectedMediaExerciseId === '' && catalogItems.length > 0) {
      setSelectedMediaExerciseId(catalogItems[0].id)
    }
  }, [catalogItems, selectedMediaExerciseId])

  useEffect(() => {
    if (selectedMediaExerciseId === '') {
      setSelectedStudioExercise(null)
      return
    }

    void loadStudioExercise(selectedMediaExerciseId)
  }, [selectedMediaExerciseId])

  useEffect(() => {
    return () => {
      if (draftMediaPreview) {
        URL.revokeObjectURL(draftMediaPreview.objectUrl)
      }
    }
  }, [draftMediaPreview])

  async function loadUsers() {
    try {
      setIsLoadingUsers(true)
      setErrorMessage(null)
      const nextUsers = await fetchAdminUsers()
      setUsers(nextUsers)
    } catch (error) {
      setErrorMessage(
        isForbiddenError(error)
          ? 'Your account no longer has access to admin tools.'
          : getRequestErrorMessage(error, 'Unable to load users.'),
      )
    } finally {
      setIsLoadingUsers(false)
    }
  }

  async function loadCatalog(query: string) {
    try {
      setIsLoadingCatalog(true)
      setErrorMessage(null)
      const nextItems = await fetchAdminExerciseCatalog(query)
      setCatalogItems(nextItems)
    } catch (error) {
      setErrorMessage(getRequestErrorMessage(error, 'Unable to load exercise catalog management data.'))
    } finally {
      setIsLoadingCatalog(false)
    }
  }

  async function loadMediaDrafts() {
    try {
      setIsLoadingMediaDrafts(true)
      setMediaStudioError(null)
      const nextDrafts = await getExerciseMediaDrafts()
      setMediaDrafts(nextDrafts)
    } catch (error) {
      setMediaStudioError(getRequestErrorMessage(error, 'Unable to load exercise media drafts.'))
    } finally {
      setIsLoadingMediaDrafts(false)
    }
  }

  async function loadStudioExercise(exerciseId: number) {
    try {
      setIsLoadingStudioExercise(true)
      setMediaStudioError(null)
      const nextStudioExercise = await getExerciseMediaStudioExercise(exerciseId)
      setSelectedStudioExercise(nextStudioExercise)
    } catch (error) {
      setMediaStudioError(getRequestErrorMessage(error, 'Unable to load exercise media studio details.'))
      setSelectedStudioExercise(null)
    } finally {
      setIsLoadingStudioExercise(false)
    }
  }

  async function handleRoleChange(userId: number, role: AdminUser['role']) {
    const actionKey = `role:${userId}`

    try {
      setPendingActions((current) => ({ ...current, [actionKey]: true }))
      setErrorMessage(null)
      setSuccessMessage(null)

      const updatedUser = await updateAdminUserRole(userId, { role })
      setUsers((current) => current.map((user) => (user.id === userId ? updatedUser : user)))
      setSuccessMessage(`Updated ${updatedUser.email} to ${updatedUser.role}.`)
    } catch (error) {
      setErrorMessage(getRequestErrorMessage(error, 'Unable to update user role.'))
    } finally {
      setPendingActions((current) => ({ ...current, [actionKey]: false }))
    }
  }

  async function handleStatusToggle(userId: number, isActive: boolean) {
    const actionKey = `status:${userId}`

    try {
      setPendingActions((current) => ({ ...current, [actionKey]: true }))
      setErrorMessage(null)
      setSuccessMessage(null)

      const updatedUser = await updateAdminUserStatus(userId, { isActive })
      setUsers((current) => current.map((user) => (user.id === userId ? updatedUser : user)))
      setSuccessMessage(`${updatedUser.email} is now ${updatedUser.isActive ? 'active' : 'inactive'}.`)
    } catch (error) {
      setErrorMessage(getRequestErrorMessage(error, 'Unable to update account status.'))
    } finally {
      setPendingActions((current) => ({ ...current, [actionKey]: false }))
    }
  }

  async function handleResetPasswordSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (!resetPasswordUser) {
      return
    }

    const actionKey = `password:${resetPasswordUser.id}`
    const newPassword = resetPasswordForm.newPassword
    const confirmPassword = resetPasswordForm.confirmPassword

    if (newPassword.trim().length < 8) {
      setResetPasswordError('Password must be at least 8 characters long.')
      return
    }

    if (newPassword !== confirmPassword) {
      setResetPasswordError('New password and confirmation do not match.')
      return
    }

    try {
      setPendingActions((current) => ({ ...current, [actionKey]: true }))
      setErrorMessage(null)
      setSuccessMessage(null)
      setResetPasswordError(null)

      const response = await resetAdminUserPassword(resetPasswordUser.id, { newPassword })
      setSuccessMessage(response.message)
      setResetPasswordForm({ newPassword: '', confirmPassword: '' })
      setResetPasswordUser(null)
    } catch (error) {
      setResetPasswordError(getRequestErrorMessage(error, 'Unable to reset password.'))
    } finally {
      setPendingActions((current) => ({ ...current, [actionKey]: false }))
    }
  }

  async function handleSyncCatalog() {
    const actionKey = 'catalog:sync'

    try {
      setPendingActions((current) => ({ ...current, [actionKey]: true }))
      setErrorMessage(null)
      setSuccessMessage(null)

      const result = await syncAdminExerciseCatalogFromWger()
      await loadCatalog(deferredCatalogSearch)
      setSuccessMessage(result.message)
    } catch (error) {
      setErrorMessage(getRequestErrorMessage(error, 'Unable to sync the exercise catalog from Wger.'))
    } finally {
      setPendingActions((current) => ({ ...current, [actionKey]: false }))
    }
  }

  async function handleRefreshMediaStudio() {
    const actionKey = 'media-studio:refresh'

    try {
      setPendingActions((current) => ({ ...current, [actionKey]: true }))
      await loadMediaDrafts()

      if (selectedMediaExerciseId !== '') {
        await loadStudioExercise(selectedMediaExerciseId)
      }
    } finally {
      setPendingActions((current) => ({ ...current, [actionKey]: false }))
    }
  }

  async function handleCreateMediaDraft() {
    if (selectedMediaExerciseId === '') {
      setMediaStudioError('Select an exercise before creating a draft.')
      return
    }

    const actionKey = `media-studio:create:${selectedMediaExerciseId}`
    const payload: CreateExerciseMediaDraftRequest = { mediaType: selectedMediaType }

    try {
      setPendingActions((current) => ({ ...current, [actionKey]: true }))
      setErrorMessage(null)
      setSuccessMessage(null)
      setMediaStudioError(null)

      const draft = await createExerciseMediaDraft(selectedMediaExerciseId, payload)
      await loadMediaDrafts()
      await loadStudioExercise(selectedMediaExerciseId)
      setPromptDraft(draft)
      setSuccessMessage(`Created ${draft.mediaType.toLowerCase()} draft for ${draft.exerciseName}.`)
    } catch (error) {
      setMediaStudioError(getRequestErrorMessage(error, 'Unable to create an exercise media draft.'))
    } finally {
      setPendingActions((current) => ({ ...current, [actionKey]: false }))
    }
  }

  async function handleOpenPromptDraft(draftId: number) {
    const actionKey = `media-studio:view:${draftId}`

    try {
      setPendingActions((current) => ({ ...current, [actionKey]: true }))
      setMediaStudioError(null)
      const draft = await getExerciseMediaDraft(draftId)
      setDraftMediaPreview((current) => (current?.draftId === draft.id ? current : null))
      setPromptDraft(draft)
    } catch (error) {
      setMediaStudioError(getRequestErrorMessage(error, 'Unable to load the draft prompt.'))
    } finally {
      setPendingActions((current) => ({ ...current, [actionKey]: false }))
    }
  }

  async function handleCopyPrompt() {
    if (!promptDraft) {
      return
    }

    if (!navigator.clipboard) {
      setMediaStudioError('Clipboard access is not available in this browser context.')
      return
    }

    const actionKey = `media-studio:copy:${promptDraft.id}`

    try {
      setPendingActions((current) => ({ ...current, [actionKey]: true }))
      setMediaStudioError(null)
      await navigator.clipboard.writeText(promptDraft.promptText)
      setSuccessMessage(`Copied prompt for ${promptDraft.exerciseName}.`)
    } catch (error) {
      setMediaStudioError(getRequestErrorMessage(error, 'Unable to copy the draft prompt.'))
    } finally {
      setPendingActions((current) => ({ ...current, [actionKey]: false }))
    }
  }

  async function handlePreviewDraftMedia(
    draft: ExerciseMediaDraftResponse,
    kind: DraftMediaPreviewKind,
  ) {
    const actionKey = `media-studio:preview-${kind}:${draft.id}`

    try {
      setPendingActions((current) => ({ ...current, [actionKey]: true }))
      setMediaStudioError(null)
      setPromptDraft(draft)

      const blob = kind === 'video'
        ? await getExerciseMediaDraftVideo(draft.id)
        : await getExerciseMediaDraftThumbnail(draft.id)
      if (blob.size === 0) {
        throw new Error('Draft media response was empty.')
      }

      setDraftMediaPreview({
        draftId: draft.id,
        kind,
        objectUrl: URL.createObjectURL(blob),
      })
    } catch (error) {
      setMediaStudioError(getRequestErrorMessage(error, `Unable to load the private draft ${kind}.`))
    } finally {
      setPendingActions((current) => ({ ...current, [actionKey]: false }))
    }
  }

  async function refreshMediaStudioAfterDraftAction(draft: ExerciseMediaDraftResponse) {
    await loadMediaDrafts()

    if (selectedMediaExerciseId !== '') {
      await loadStudioExercise(selectedMediaExerciseId)
    }

    setPromptDraft((current) => (current?.id === draft.id ? draft : current))
    setDraftMediaPreview((current) => {
      if (current?.draftId !== draft.id) {
        return current
      }

      const stillAvailable = current.kind === 'video'
        ? Boolean(draft.generatedVideoUrl)
        : Boolean(draft.generatedThumbnailUrl)
      return stillAvailable ? current : null
    })
  }

  async function handleApproveMediaDraft(draft: ExerciseMediaDraftResponse) {
    const reviewNotes = window.prompt('Optional review notes:', draft.reviewNotes ?? '')
    if (reviewNotes === null) {
      return
    }

    const actionKey = `media-studio:approve:${draft.id}`
    const payload: ReviewExerciseMediaDraftRequest = { reviewNotes }

    try {
      setPendingActions((current) => ({ ...current, [actionKey]: true }))
      setErrorMessage(null)
      setSuccessMessage(null)
      setMediaStudioError(null)

      const updatedDraft = await approveExerciseMediaDraft(draft.id, payload)
      await refreshMediaStudioAfterDraftAction(updatedDraft)
      setSuccessMessage(`Approved ${updatedDraft.mediaType.toLowerCase()} draft for ${updatedDraft.exerciseName}.`)
    } catch (error) {
      setMediaStudioError(getRequestErrorMessage(error, 'Unable to approve the exercise media draft.'))
    } finally {
      setPendingActions((current) => ({ ...current, [actionKey]: false }))
    }
  }

  async function handleRejectMediaDraft(draft: ExerciseMediaDraftResponse) {
    const rejectionReason = window.prompt('Optional rejection reason:', draft.rejectionReason ?? '')
    if (rejectionReason === null) {
      return
    }

    const actionKey = `media-studio:reject:${draft.id}`
    const payload: RejectExerciseMediaDraftRequest = { rejectionReason }

    try {
      setPendingActions((current) => ({ ...current, [actionKey]: true }))
      setErrorMessage(null)
      setSuccessMessage(null)
      setMediaStudioError(null)

      const updatedDraft = await rejectExerciseMediaDraft(draft.id, payload)
      await refreshMediaStudioAfterDraftAction(updatedDraft)
      setSuccessMessage(`Rejected ${updatedDraft.mediaType.toLowerCase()} draft for ${updatedDraft.exerciseName}.`)
    } catch (error) {
      setMediaStudioError(getRequestErrorMessage(error, 'Unable to reject the exercise media draft.'))
    } finally {
      setPendingActions((current) => ({ ...current, [actionKey]: false }))
    }
  }

  async function handlePublishMediaDraft(draft: ExerciseMediaDraftResponse) {
    const confirmed = window.confirm(
      `Publish this draft for ${draft.exerciseName}? Generated media will replace the current local media override for this exercise.`,
    )
    if (!confirmed) {
      return
    }

    const actionKey = `media-studio:publish:${draft.id}`

    try {
      setPendingActions((current) => ({ ...current, [actionKey]: true }))
      setErrorMessage(null)
      setSuccessMessage(null)
      setMediaStudioError(null)

      const updatedDraft = await publishExerciseMediaDraft(draft.id)
      await refreshMediaStudioAfterDraftAction(updatedDraft)
      setSuccessMessage(`Published generated media for ${updatedDraft.exerciseName}.`)
    } catch (error) {
      setMediaStudioError(getRequestErrorMessage(error, 'Unable to publish the exercise media draft.'))
    } finally {
      setPendingActions((current) => ({ ...current, [actionKey]: false }))
    }
  }

  async function handleGenerateMediaDraft(draft: ExerciseMediaDraftResponse) {
    const confirmed = window.confirm(
      `Start AI video generation for ${draft.exerciseName}? This action may incur provider usage costs.`,
    )
    if (!confirmed) {
      return
    }

    const actionKey = `media-studio:generate:${draft.id}`

    try {
      setPendingActions((current) => ({ ...current, [actionKey]: true }))
      setErrorMessage(null)
      setSuccessMessage(null)
      setMediaStudioError(null)

      const updatedDraft = await generateExerciseMediaDraft(draft.id)
      await refreshMediaStudioAfterDraftAction(updatedDraft)
      setSuccessMessage(`Started video generation for ${updatedDraft.exerciseName}.`)
    } catch (error) {
      setMediaStudioError(getRequestErrorMessage(error, 'Unable to start exercise media generation.'))
    } finally {
      setPendingActions((current) => ({ ...current, [actionKey]: false }))
    }
  }

  async function handleRefreshMediaDraftStatus(draft: ExerciseMediaDraftResponse) {
    const actionKey = `media-studio:refresh-status:${draft.id}`

    try {
      setPendingActions((current) => ({ ...current, [actionKey]: true }))
      setErrorMessage(null)
      setSuccessMessage(null)
      setMediaStudioError(null)

      const updatedDraft = await refreshExerciseMediaDraftStatus(draft.id)
      await refreshMediaStudioAfterDraftAction(updatedDraft)
      setSuccessMessage(
        updatedDraft.generatedVideoUrl
          ? `Generated video is ready for review for ${updatedDraft.exerciseName}.`
          : `Generation status refreshed for ${updatedDraft.exerciseName}.`,
      )
    } catch (error) {
      setMediaStudioError(getRequestErrorMessage(error, 'Unable to refresh exercise media generation status.'))
    } finally {
      setPendingActions((current) => ({ ...current, [actionKey]: false }))
    }
  }

  async function handleCatalogStatusToggle(item: AdminExerciseCatalogItem) {
    const actionKey = `catalog:status:${item.id}`

    try {
      setPendingActions((current) => ({ ...current, [actionKey]: true }))
      setErrorMessage(null)
      setSuccessMessage(null)

      const updatedItem = await updateAdminExerciseCatalogItem(item.id, {
        name: item.name,
        instructions: item.instructions ?? '',
        thumbnailUrl: item.thumbnailUrl ?? '',
        videoUrl: item.videoUrl ?? '',
        isActive: !item.isActive,
      })

      setCatalogItems((current) => current.map((entry) => (entry.id === item.id ? updatedItem : entry)))
      setSuccessMessage(`${updatedItem.name} is now ${updatedItem.isActive ? 'active' : 'inactive'}.`)
    } catch (error) {
      setErrorMessage(getRequestErrorMessage(error, 'Unable to update exercise status.'))
    } finally {
      setPendingActions((current) => ({ ...current, [actionKey]: false }))
    }
  }

  async function handleCatalogSave(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (!editingCatalogItem) {
      return
    }

    const actionKey = `catalog:save:${editingCatalogItem.id}`

    try {
      setPendingActions((current) => ({ ...current, [actionKey]: true }))
      setCatalogFormError(null)
      setErrorMessage(null)
      setSuccessMessage(null)

      const updatedItem = await updateAdminExerciseCatalogItem(editingCatalogItem.id, catalogForm)
      setCatalogItems((current) => current.map((entry) => (entry.id === updatedItem.id ? updatedItem : entry)))
      setEditingCatalogItem(updatedItem)
      setCatalogForm(mapCatalogItemToForm(updatedItem))
      setSuccessMessage(`Saved catalog changes for ${updatedItem.name}.`)
    } catch (error) {
      setCatalogFormError(getRequestErrorMessage(error, 'Unable to save exercise catalog changes.'))
    } finally {
      setPendingActions((current) => ({ ...current, [actionKey]: false }))
    }
  }

  async function handleResetCatalogItem() {
    if (!editingCatalogItem) {
      return
    }

    const confirmed = window.confirm('Reset this catalog item to the provider version? Local overrides will be removed.')
    if (!confirmed) {
      return
    }

    const actionKey = `catalog:reset:${editingCatalogItem.id}`

    try {
      setPendingActions((current) => ({ ...current, [actionKey]: true }))
      setCatalogFormError(null)
      setErrorMessage(null)
      setSuccessMessage(null)

      const updatedItem = await resetAdminExerciseCatalogItem(editingCatalogItem.id)
      setCatalogItems((current) => current.map((entry) => (entry.id === updatedItem.id ? updatedItem : entry)))
      setEditingCatalogItem(updatedItem)
      setCatalogForm(mapCatalogItemToForm(updatedItem))
      setSuccessMessage(`Reset ${updatedItem.name} to provider values.`)
    } catch (error) {
      setCatalogFormError(getRequestErrorMessage(error, 'Unable to reset this catalog item.'))
    } finally {
      setPendingActions((current) => ({ ...current, [actionKey]: false }))
    }
  }

  function openResetPasswordDialog(user: AdminUser) {
    setResetPasswordUser(user)
    setResetPasswordForm({ newPassword: '', confirmPassword: '' })
    setResetPasswordError(null)
    setErrorMessage(null)
    setSuccessMessage(null)
  }

  function closeResetPasswordDialog() {
    setResetPasswordUser(null)
    setResetPasswordForm({ newPassword: '', confirmPassword: '' })
    setResetPasswordError(null)
  }

  function openCatalogEditor(item: AdminExerciseCatalogItem) {
    setEditingCatalogItem(item)
    setCatalogForm(mapCatalogItemToForm(item))
    setCatalogFormError(null)
    setErrorMessage(null)
    setSuccessMessage(null)
  }

  function closeCatalogEditor() {
    setEditingCatalogItem(null)
    setCatalogForm(initialCatalogFormState())
    setCatalogFormError(null)
  }

  function closePromptDialog() {
    setPromptDraft(null)
    setDraftMediaPreview(null)
  }

  function openDraftExercise(exerciseId: number) {
    setSelectedMediaExerciseId(exerciseId)
  }

  return (
    <main className="page-shell admin-shell">
      <section className="hero-panel admin-hero-panel">
        <div className="hero-copy admin-hero-copy">
          <span className="eyebrow">Admin</span>
          <h1>Operations & Catalog Management</h1>
          <p className="hero-text">
            Manage user access, protect local exercise overrides, and control catalog sync without changing the workout flow.
          </p>
        </div>

        <div className="stats-grid">
          <article className="stat-card">
            <span className="stat-label">Total Users</span>
            <strong>{users.length}</strong>
            <span className="stat-subtext">Accounts currently stored in the app database.</span>
          </article>
          <article className="stat-card">
            <span className="stat-label">Catalog Items</span>
            <strong>{catalogItems.length}</strong>
            <span className="stat-subtext">Exercises available for admin review.</span>
          </article>
          <article className="stat-card">
            <span className="stat-label">Manual Overrides</span>
            <strong>{catalogItems.filter((item) => item.isManuallyEdited).length}</strong>
            <span className="stat-subtext">Exercises protected from upstream overwrite.</span>
          </article>
          <article className="stat-card">
            <span className="stat-label">Current Session</span>
            <strong>{authState?.user.role ?? 'Unknown'}</strong>
            <span className="stat-subtext">Your own access level for this session.</span>
          </article>
        </div>
      </section>

      {(successMessage || errorMessage) ? (
        <div className="feedback-stack">
          {successMessage ? <p className="feedback success">{successMessage}</p> : null}
          {errorMessage ? <p className="feedback error">{errorMessage}</p> : null}
        </div>
      ) : null}

      <section className="content-grid admin-grid">
        <div className="panel panel-span-2" id="media-studio">
          <div className="panel-header media-studio-panel-header">
            <div>
              <h2>Exercise Media Studio</h2>
              <p>Create AI-ready draft prompts, inspect current media state, and review draft records before anything is published.</p>
            </div>
            <div className="toolbar-actions">
              <button
                type="button"
                className="ghost-button"
                onClick={() => void handleRefreshMediaStudio()}
                disabled={pendingActions['media-studio:refresh'] ?? false}
              >
                <RefreshCw aria-hidden="true" focusable="false" strokeWidth={1.9} />
                {pendingActions['media-studio:refresh'] ?? false ? 'Refreshing...' : 'Refresh'}
              </button>
            </div>
          </div>

          <div className="media-studio-toolbar">
            <label className="field">
              <span>Exercise</span>
              <select
                className="select-input admin-select"
                value={selectedMediaExerciseId}
                onChange={(event) => setSelectedMediaExerciseId(event.target.value ? Number(event.target.value) : '')}
                disabled={isLoadingCatalog || catalogItems.length === 0}
              >
                <option value="">Select an exercise</option>
                {catalogItems.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.name} · {item.source}
                  </option>
                ))}
              </select>
              <small>Uses the current catalog result set from the management search below.</small>
            </label>

            <label className="field">
              <span>Draft media type</span>
              <select
                className="select-input admin-select"
                value={selectedMediaType}
                onChange={(event) => setSelectedMediaType(event.target.value as ExerciseMediaDraftMediaType)}
              >
                {mediaDraftTypeOptions.map((mediaType) => (
                  <option key={mediaType} value={mediaType}>
                    {mediaType}
                  </option>
                ))}
              </select>
            </label>

            <div className="media-studio-toolbar-actions">
              <button
                type="button"
                className="primary-button"
                onClick={() => void handleCreateMediaDraft()}
                disabled={
                  selectedMediaExerciseId === '' ||
                  (pendingActions[`media-studio:create:${selectedMediaExerciseId}`] ?? false)
                }
              >
                <Sparkles aria-hidden="true" focusable="false" strokeWidth={1.9} />
                {selectedMediaExerciseId !== '' && (pendingActions[`media-studio:create:${selectedMediaExerciseId}`] ?? false)
                  ? 'Creating...'
                  : 'Create draft'}
              </button>
            </div>
          </div>

          {mediaStudioError ? <p className="feedback error">{mediaStudioError}</p> : null}

          {isLoadingStudioExercise ? (
            <StateCard title="Loading studio details" description="Fetching current media state for the selected exercise." loading />
          ) : selectedStudioExercise ? (
            <div className="media-studio-summary-grid">
              <article className="exercise-help-card">
                <div className="panel-header media-studio-card-header">
                  <div>
                    <h3>{selectedStudioExercise.name}</h3>
                    <p>
                      {selectedStudioExercise.source}
                      {selectedStudioExercise.externalId ? ` · External ID ${selectedStudioExercise.externalId}` : ''}
                    </p>
                  </div>
                  <div className="media-studio-badge-row">
                    <span
                      className={
                        selectedStudioExercise.isActive ? 'status-pill status-pill-active' : 'status-pill status-pill-inactive'
                      }
                    >
                      {selectedStudioExercise.isActive ? 'Active' : 'Inactive'}
                    </span>
                    {selectedStudioExercise.isManuallyEdited ? (
                      <span className="status-pill media-studio-status-pill">Manual override</span>
                    ) : null}
                  </div>
                </div>

                <div className="media-studio-meta-grid">
                  <span className="info-pill">{selectedStudioExercise.providerName}</span>
                  {selectedStudioExercise.primaryMuscle ? (
                    <span className="info-pill">{formatCatalogLabel(selectedStudioExercise.primaryMuscle)}</span>
                  ) : null}
                  {selectedStudioExercise.equipment ? (
                    <span className="info-pill">{formatCatalogLabel(selectedStudioExercise.equipment)}</span>
                  ) : null}
                  {selectedStudioExercise.difficulty ? (
                    <span className="info-pill">{formatCatalogLabel(selectedStudioExercise.difficulty)}</span>
                  ) : null}
                </div>

                <div className="media-studio-link-grid">
                  <MediaStudioLink label="Effective thumbnail" value={selectedStudioExercise.thumbnailUrl} />
                  <MediaStudioLink label="Provider thumbnail" value={selectedStudioExercise.providerThumbnailUrl} />
                  <MediaStudioLink label="Local thumbnail override" value={selectedStudioExercise.localThumbnailUrlOverride} />
                  <MediaStudioLink label="Effective video" value={selectedStudioExercise.videoUrl} />
                  <MediaStudioLink label="Provider video" value={selectedStudioExercise.providerVideoUrl} />
                  <MediaStudioLink label="Local video override" value={selectedStudioExercise.localVideoUrlOverride} />
                  <MediaStudioValue label="Local media path" value={selectedStudioExercise.localMediaPath} />
                </div>

                {selectedStudioExercise.instructions ? (
                  <p className="media-studio-instructions">{truncateText(selectedStudioExercise.instructions, 260)}</p>
                ) : null}
              </article>

              <article className="exercise-help-card">
                <div className="panel-header media-studio-card-header">
                  <div>
                    <h3>Latest Drafts For Exercise</h3>
                    <p>Current draft history for the selected exercise.</p>
                  </div>
                </div>

                {selectedStudioExercise.latestDrafts.length === 0 ? (
                  <StateCard
                    title="No drafts for this exercise"
                    description="Create a draft to capture a source snapshot and prompt preview."
                  />
                ) : (
                  <div className="media-studio-latest-drafts">
                    {selectedStudioExercise.latestDrafts.map((draft) => (
                      <article key={draft.id} className="suggestion-card suggestion-card-compact">
                        <div className="media-studio-draft-card-header">
                          <strong>{draft.mediaType}</strong>
                          <span className={getMediaStudioStatusClassName(draft.status)}>{formatCatalogLabel(draft.status)}</span>
                        </div>
                        <p className="record-hint">
                          {formatDateTime(draft.createdAt)} · {draft.promptVersion ?? 'No version'}
                        </p>
                        <p className="media-studio-prompt-preview">{truncateText(draft.promptText, 180)}</p>
                        <MediaStudioGenerationSummary draft={draft} />
                        <MediaStudioReviewSummary draft={draft} />
                        <MediaStudioDraftActions
                          draft={draft}
                          pendingActions={pendingActions}
                          onOpenPrompt={handleOpenPromptDraft}
                          onPreviewVideo={(draft) => handlePreviewDraftMedia(draft, 'video')}
                          onPreviewThumbnail={(draft) => handlePreviewDraftMedia(draft, 'thumbnail')}
                          onGenerateDraft={handleGenerateMediaDraft}
                          onRefreshStatus={handleRefreshMediaDraftStatus}
                          onApproveDraft={handleApproveMediaDraft}
                          onRejectDraft={handleRejectMediaDraft}
                          onPublishDraft={handlePublishMediaDraft}
                        />
                      </article>
                    ))}
                  </div>
                )}
              </article>
            </div>
          ) : (
            <StateCard
              title="Select an exercise"
              description="Choose a catalog item below to inspect its current media and latest draft history."
            />
          )}

          {isLoadingMediaDrafts ? (
            <StateCard title="Loading drafts" description="Fetching exercise media drafts." loading />
          ) : mediaDrafts.length === 0 ? (
            <StateCard title="No media drafts yet" description="Create the first exercise media draft to start the review workflow." />
          ) : (
            <div className="admin-table-shell panel-scroll-region media-studio-table-shell">
              <table className="admin-table">
                <caption className="sr-only">Exercise media studio draft list.</caption>
                <thead>
                  <tr>
                    <th>Exercise</th>
                    <th>Type</th>
                    <th>Status</th>
                    <th>Provider / model</th>
                    <th>Prompt preview</th>
                    <th>Generated media</th>
                    <th>Created / updated</th>
                    <th>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {mediaDrafts.map((draft) => {
                    const isViewingDraft = pendingActions[`media-studio:view:${draft.id}`] ?? false

                    return (
                      <tr key={draft.id} className={isViewingDraft ? 'admin-row admin-row-updating' : 'admin-row'}>
                        <td className="admin-cell-strong" data-label="Exercise">
                          <div className="admin-user-cell">
                            <strong>{draft.exerciseName}</strong>
                            <span className="record-hint">
                              {draft.exerciseSource} · Draft #{draft.id}
                            </span>
                          </div>
                        </td>
                        <td data-label="Type">{draft.mediaType}</td>
                        <td data-label="Status">
                          <span className={getMediaStudioStatusClassName(draft.status)}>{formatCatalogLabel(draft.status)}</span>
                          <MediaStudioReviewSummary draft={draft} />
                        </td>
                        <td data-label="Provider / model">
                          <MediaStudioGenerationSummary draft={draft} />
                        </td>
                        <td data-label="Prompt">
                          <p className="media-studio-prompt-preview">{truncateText(draft.promptText, 160)}</p>
                        </td>
                        <td data-label="Generated media">
                          <div className="media-studio-url-stack">
                            {draft.generatedThumbnailUrl ? (
                              <span className="record-hint">Private thumbnail ready</span>
                            ) : null}
                            {draft.generatedVideoUrl ? (
                              <span className="record-hint">Private video ready</span>
                            ) : null}
                            {!draft.generatedThumbnailUrl && !draft.generatedVideoUrl ? (
                              <span className="record-hint">Not generated</span>
                            ) : null}
                          </div>
                        </td>
                        <td className="record-hint" data-label="Dates">
                          <div className="media-studio-date-stack">
                            <span>Created {formatDateTime(draft.createdAt)}</span>
                            <span>Updated {formatDateTime(draft.updatedAt)}</span>
                          </div>
                        </td>
                        <td data-label="Actions">
                          <MediaStudioDraftActions
                            draft={draft}
                            pendingActions={pendingActions}
                            onOpenExercise={openDraftExercise}
                            onOpenPrompt={handleOpenPromptDraft}
                            onPreviewVideo={(draft) => handlePreviewDraftMedia(draft, 'video')}
                            onPreviewThumbnail={(draft) => handlePreviewDraftMedia(draft, 'thumbnail')}
                            onGenerateDraft={handleGenerateMediaDraft}
                            onRefreshStatus={handleRefreshMediaDraftStatus}
                            onApproveDraft={handleApproveMediaDraft}
                            onRejectDraft={handleRejectMediaDraft}
                            onPublishDraft={handlePublishMediaDraft}
                          />
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>

        <div className="panel panel-span-2">
          <div className="panel-header">
            <div>
              <h2>Users</h2>
              <p>Manage account role and access status while keeping the current admin safeguards intact.</p>
            </div>
          </div>

          {isLoadingUsers ? (
            <StateCard title="Loading users" description="Fetching current user accounts." loading />
          ) : users.length === 0 ? (
            <StateCard title="No users found" description="User accounts will appear here once people start registering." />
          ) : (
            <div className="admin-table-shell panel-scroll-region">
              <table className="admin-table">
                <caption className="sr-only">User administration table with role and account status controls.</caption>
                <thead>
                  <tr>
                    <th>Email</th>
                    <th>Full Name</th>
                    <th>Display Name</th>
                    <th>Role</th>
                    <th>Status</th>
                    <th>Created</th>
                    <th>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {users.map((user) => {
                    const isRoleUpdating = pendingActions[`role:${user.id}`] ?? false
                    const isStatusUpdating = pendingActions[`status:${user.id}`] ?? false
                    const isPasswordResetting = pendingActions[`password:${user.id}`] ?? false
                    const isUpdating = isRoleUpdating || isStatusUpdating || isPasswordResetting

                    return (
                      <tr key={user.id} className={isUpdating ? 'admin-row admin-row-updating' : 'admin-row'}>
                        <td className="admin-cell-strong" data-label="User">
                          <div className="admin-user-cell">
                            <strong>{user.email}</strong>
                            <span className="record-hint">Created {formatDate(user.createdAt)}</span>
                          </div>
                        </td>
                        <td data-label="Full name">{user.fullName || 'Not set'}</td>
                        <td data-label="Display name">{user.displayName || 'Not set'}</td>
                        <td data-label="Role">
                          <label className="admin-select-label">
                            <span className="sr-only">Role for {user.email}</span>
                            <span className="admin-field-hint">Role</span>
                            <select
                              className="select-input admin-select"
                              value={user.role}
                              disabled={isUpdating}
                              onChange={(event) => void handleRoleChange(user.id, event.target.value as AdminUser['role'])}
                            >
                              <option value="User">User</option>
                              <option value="Admin">Admin</option>
                            </select>
                          </label>
                        </td>
                        <td data-label="Status">
                          <span className={user.isActive ? 'status-pill status-pill-active' : 'status-pill status-pill-inactive'}>
                            {user.isActive ? 'Active' : 'Inactive'}
                          </span>
                        </td>
                        <td className="record-hint" data-label="Created">{formatDate(user.createdAt)}</td>
                        <td data-label="Actions">
                          <div className="admin-actions">
                            <button type="button" className="ghost-button" disabled={isUpdating} onClick={() => openResetPasswordDialog(user)}>
                              <KeyRound aria-hidden="true" focusable="false" strokeWidth={1.9} />
                              {isPasswordResetting ? 'Saving...' : 'Reset Password'}
                            </button>
                            <button
                              type="button"
                              className={user.isActive ? 'ghost-button subtle-danger-button' : 'ghost-button'}
                              disabled={isUpdating}
                              onClick={() => void handleStatusToggle(user.id, !user.isActive)}
                            >
                              {isStatusUpdating ? 'Saving...' : user.isActive ? 'Deactivate' : 'Activate'}
                            </button>
                          </div>
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>

        <div className="panel panel-span-2">
          <div className="panel-header">
            <div>
              <h2>Exercise Catalog Management</h2>
              <p>Control active catalog items, preserve local overrides, and sync provider data without losing admin edits.</p>
            </div>
            <div className="toolbar-actions">
              <button
                type="button"
                className="ghost-button"
                onClick={() => void handleSyncCatalog()}
                disabled={pendingActions['catalog:sync'] ?? false}
              >
                <RefreshCw aria-hidden="true" focusable="false" strokeWidth={1.9} />
                {pendingActions['catalog:sync'] ?? false ? 'Syncing...' : 'Sync Wger'}
              </button>
            </div>
          </div>

          <div className="filter-toolbar filter-toolbar-workouts">
            <label className="field">
              <span>Search catalog</span>
              <input
                type="search"
                value={catalogSearch}
                onChange={(event) => setCatalogSearch(event.target.value)}
                placeholder="Search by name, muscle, or equipment"
              />
            </label>
          </div>

          {isLoadingCatalog ? (
            <StateCard title="Loading catalog" description="Fetching exercise catalog management data." loading />
          ) : catalogItems.length === 0 ? (
            <StateCard title="No catalog items found" description="Sync or seed the exercise catalog to manage it here." />
          ) : (
            <div className="admin-table-shell panel-scroll-region">
              <table className="admin-table">
                <caption className="sr-only">Exercise catalog management table.</caption>
                <thead>
                  <tr>
                    <th>Name</th>
                    <th>Source</th>
                    <th>Primary muscle</th>
                    <th>Equipment</th>
                    <th>Status</th>
                    <th>Last synced</th>
                    <th>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {visibleCatalogItems.map((item) => {
                    const isStatusUpdating = pendingActions[`catalog:status:${item.id}`] ?? false

                    return (
                      <tr key={item.id} className={isStatusUpdating ? 'admin-row admin-row-updating' : 'admin-row'}>
                        <td className="admin-cell-strong" data-label="Exercise">
                          <div className="admin-user-cell">
                            <strong>{item.name}</strong>
                            <span className="record-hint">
                              {item.isManuallyEdited ? 'Local override active' : `Provider: ${item.providerName}`}
                            </span>
                          </div>
                        </td>
                        <td data-label="Source">{item.source}</td>
                        <td data-label="Primary muscle">{item.primaryMuscle ? formatCatalogLabel(item.primaryMuscle) : 'Not set'}</td>
                        <td data-label="Equipment">{item.equipment ? formatCatalogLabel(item.equipment) : 'Not set'}</td>
                        <td data-label="Status">
                          <span className={item.isActive ? 'status-pill status-pill-active' : 'status-pill status-pill-inactive'}>
                            {item.isActive ? 'Active' : 'Inactive'}
                          </span>
                        </td>
                        <td className="record-hint" data-label="Last synced">{item.lastSyncedAt ? formatDate(item.lastSyncedAt) : 'Never'}</td>
                        <td data-label="Actions">
                          <div className="admin-actions">
                            <button type="button" className="ghost-button" onClick={() => openCatalogEditor(item)}>
                              <Pencil aria-hidden="true" focusable="false" strokeWidth={1.9} />
                              Edit
                            </button>
                            <button
                              type="button"
                              className={item.isActive ? 'ghost-button subtle-danger-button' : 'ghost-button'}
                              disabled={isStatusUpdating}
                              onClick={() => void handleCatalogStatusToggle(item)}
                            >
                              {isStatusUpdating ? 'Saving...' : item.isActive ? 'Deactivate' : 'Activate'}
                            </button>
                          </div>
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
              {isMobileCatalogList ? (
                <div className="catalog-mobile-load-more">
                  <p>
                    Showing {catalogVisibleCount} of {catalogItems.length}
                  </p>
                  {canLoadMoreCatalogItems ? (
                    <button
                      type="button"
                      className="ghost-button"
                      onClick={() => setMobileCatalogVisibleCount((current) => current + catalogMobilePageSize)}
                    >
                      Load 5 more
                    </button>
                  ) : null}
                </div>
              ) : null}
            </div>
          )}
        </div>

      </section>

      {resetPasswordUser ? (
        <div className="modal-backdrop" role="presentation" onClick={closeResetPasswordDialog}>
          <div
            className="modal-panel"
            role="dialog"
            aria-modal="true"
            aria-labelledby="reset-password-title"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="panel-header modal-header">
              <div>
                <h2 id="reset-password-title">Reset Password</h2>
                <p>Set a new password for {resetPasswordUser.email}. The password will not be shown again after submission.</p>
              </div>
            </div>

            <form className="weight-form" onSubmit={handleResetPasswordSubmit}>
              <label className="field">
                <span>New password</span>
                <input
                  type="password"
                  minLength={8}
                  value={resetPasswordForm.newPassword}
                  onChange={(event) => setResetPasswordForm((current) => ({ ...current, newPassword: event.target.value }))}
                  placeholder="At least 8 characters"
                />
              </label>

              <label className="field">
                <span>Confirm password</span>
                <input
                  type="password"
                  minLength={8}
                  value={resetPasswordForm.confirmPassword}
                  onChange={(event) => setResetPasswordForm((current) => ({ ...current, confirmPassword: event.target.value }))}
                  placeholder="Re-enter the new password"
                />
              </label>

              {resetPasswordError ? <p className="feedback error">{resetPasswordError}</p> : null}

              <div className="action-row">
                <button type="button" className="ghost-button" onClick={closeResetPasswordDialog}>
                  Cancel
                </button>
                <button type="submit" className="primary-button" disabled={pendingActions[`password:${resetPasswordUser.id}`] ?? false}>
                  {pendingActions[`password:${resetPasswordUser.id}`] ?? false ? 'Resetting...' : 'Reset password'}
                </button>
              </div>
            </form>
          </div>
        </div>
      ) : null}

      {editingCatalogItem ? (
        <div className="modal-backdrop" role="presentation" onClick={closeCatalogEditor}>
          <div
            className="modal-panel admin-catalog-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="catalog-editor-title"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="panel-header modal-header">
              <div>
                <h2 id="catalog-editor-title">Edit Exercise Catalog Item</h2>
                <p>
                  Provider source: {editingCatalogItem.source}
                  {editingCatalogItem.externalId ? ` · External ID ${editingCatalogItem.externalId}` : ''}
                </p>
              </div>
            </div>

            <form className="weight-form" onSubmit={handleCatalogSave}>
              <label className="field">
                <span>Display name</span>
                <input
                  type="text"
                  maxLength={160}
                  value={catalogForm.name}
                  onChange={(event) => setCatalogForm((current) => ({ ...current, name: event.target.value }))}
                  placeholder="Exercise display name"
                />
                <small>Provider name: {editingCatalogItem.providerName}</small>
              </label>

              <label className="field">
                <span>Instructions</span>
                <textarea
                  className="text-area"
                  rows={5}
                  maxLength={6000}
                  value={catalogForm.instructions}
                  onChange={(event) => setCatalogForm((current) => ({ ...current, instructions: event.target.value }))}
                  placeholder="Short training cues or setup notes"
                />
              </label>

              <label className="field">
                <span>Thumbnail URL</span>
                <input
                  type="url"
                  maxLength={500}
                  value={catalogForm.thumbnailUrl}
                  onChange={(event) => setCatalogForm((current) => ({ ...current, thumbnailUrl: event.target.value }))}
                  placeholder="https://..."
                />
              </label>

              <label className="field">
                <span>Video URL</span>
                <input
                  type="url"
                  maxLength={500}
                  value={catalogForm.videoUrl}
                  onChange={(event) => setCatalogForm((current) => ({ ...current, videoUrl: event.target.value }))}
                  placeholder="https://..."
                />
              </label>

              <label className="field-checkbox">
                <input
                  type="checkbox"
                  checked={catalogForm.isActive}
                  onChange={(event) => setCatalogForm((current) => ({ ...current, isActive: event.target.checked }))}
                />
                <span>Keep this exercise active in normal library and picker results</span>
              </label>

              <div className="admin-catalog-meta">
                <span className="record-hint">Last synced: {editingCatalogItem.lastSyncedAt ? formatDate(editingCatalogItem.lastSyncedAt) : 'Never'}</span>
                <span className="record-hint">Last edited: {editingCatalogItem.lastEditedAt ? formatDate(editingCatalogItem.lastEditedAt) : 'Never'}</span>
              </div>

              {catalogFormError ? <p className="feedback error">{catalogFormError}</p> : null}

              <div className="action-row">
                <button type="button" className="ghost-button" onClick={closeCatalogEditor}>
                  Cancel
                </button>
                <button
                  type="button"
                  className="ghost-button"
                  onClick={() => void handleResetCatalogItem()}
                  disabled={pendingActions[`catalog:reset:${editingCatalogItem.id}`] ?? false}
                >
                  {pendingActions[`catalog:reset:${editingCatalogItem.id}`] ?? false ? 'Resetting...' : 'Reset to provider'}
                </button>
                <button
                  type="submit"
                  className="primary-button"
                  disabled={pendingActions[`catalog:save:${editingCatalogItem.id}`] ?? false}
                >
                  {pendingActions[`catalog:save:${editingCatalogItem.id}`] ?? false ? 'Saving...' : 'Save changes'}
                </button>
              </div>
            </form>
          </div>
        </div>
      ) : null}

      {promptDraft ? (
        <div className="modal-backdrop" role="presentation" onClick={closePromptDialog}>
          <div
            className="modal-panel admin-catalog-modal media-studio-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="media-studio-prompt-title"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="panel-header modal-header">
              <div>
                <h2 id="media-studio-prompt-title">{promptDraft.exerciseName} Draft Prompt</h2>
                <p>
                  {promptDraft.mediaType} · {formatCatalogLabel(promptDraft.status)} · {promptDraft.promptVersion ?? 'No version'}
                </p>
              </div>
              <button type="button" className="ghost-button compact-button" onClick={closePromptDialog}>
                Close
              </button>
            </div>

            <div className="media-studio-modal-content">
              {mediaStudioError ? <p className="feedback error">{mediaStudioError}</p> : null}

              <div className="media-studio-meta-grid">
                <span className={getMediaStudioStatusClassName(promptDraft.status)}>{formatCatalogLabel(promptDraft.status)}</span>
                <span className="info-pill">{promptDraft.exerciseSource}</span>
                <span className="info-pill">Created {formatDateTime(promptDraft.createdAt)}</span>
                <span className="info-pill">Updated {formatDateTime(promptDraft.updatedAt)}</span>
              </div>

              <MediaStudioReviewSummary draft={promptDraft} />
              <MediaStudioGenerationSummary draft={promptDraft} />

              {draftMediaPreview?.draftId === promptDraft.id ? (
                <div className="media-studio-private-preview">
                  <span className="admin-field-hint">Private admin preview</span>
                  {draftMediaPreview.kind === 'video' ? (
                    <video controls muted preload="metadata" src={draftMediaPreview.objectUrl} />
                  ) : (
                    <img src={draftMediaPreview.objectUrl} alt={`${promptDraft.exerciseName} generated draft thumbnail`} />
                  )}
                </div>
              ) : null}

              <div className="media-studio-modal-actions">
                <MediaStudioDraftActions
                  draft={promptDraft}
                  pendingActions={pendingActions}
                  onClose={closePromptDialog}
                  onCopyPrompt={handleCopyPrompt}
                  onPreviewVideo={(draft) => handlePreviewDraftMedia(draft, 'video')}
                  onPreviewThumbnail={(draft) => handlePreviewDraftMedia(draft, 'thumbnail')}
                  onGenerateDraft={handleGenerateMediaDraft}
                  onRefreshStatus={handleRefreshMediaDraftStatus}
                  onApproveDraft={handleApproveMediaDraft}
                  onRejectDraft={handleRejectMediaDraft}
                  onPublishDraft={handlePublishMediaDraft}
                />
              </div>

              <pre className="media-studio-prompt-block">{promptDraft.promptText}</pre>

              {promptDraft.sourceSnapshotJson ? (
                <details className="media-studio-snapshot-panel">
                  <summary>Source snapshot</summary>
                  <pre className="media-studio-snapshot-block">{promptDraft.sourceSnapshotJson}</pre>
                </details>
              ) : null}
            </div>
          </div>
        </div>
      ) : null}
    </main>
  )
}

function mapCatalogItemToForm(item: AdminExerciseCatalogItem): UpdateExerciseCatalogItemPayload {
  return {
    name: item.name,
    instructions: item.instructions ?? '',
    thumbnailUrl: item.thumbnailUrl ?? '',
    videoUrl: item.videoUrl ?? '',
    isActive: item.isActive,
  }
}

function formatCatalogLabel(value: string) {
  return value
    .split(/[\s,_-]+/)
    .filter(Boolean)
    .map((part) => part[0]?.toUpperCase() + part.slice(1))
    .join(' ')
}

function formatDateTime(date: string) {
  return new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  }).format(new Date(date))
}

function truncateText(value: string, maxLength: number) {
  if (value.length <= maxLength) {
    return value
  }

  return `${value.slice(0, maxLength).trimEnd()}...`
}

function formatGenerationLabel(draft: ExerciseMediaDraftResponse) {
  if (draft.generationProvider && draft.generationModel) {
    return `${draft.generationProvider} / ${draft.generationModel}`
  }

  if (draft.generationProvider) {
    return draft.generationProvider
  }

  if (draft.generationModel) {
    return draft.generationModel
  }

  return 'Not set'
}

function getMediaStudioStatusClassName(status: string) {
  switch (status) {
    case 'Queued':
      return 'status-pill status-pill-queued'
    case 'Generating':
      return 'status-pill status-pill-generating'
    case 'Generated':
      return 'status-pill status-pill-generated'
    case 'NeedsReview':
      return 'status-pill status-pill-needs-review'
    case 'Approved':
      return 'status-pill status-pill-approved'
    case 'Rejected':
      return 'status-pill status-pill-rejected'
    case 'Published':
      return 'status-pill status-pill-published'
    case 'Failed':
      return 'status-pill status-pill-failed'
    case 'Archived':
      return 'status-pill status-pill-archived'
    default:
      return 'status-pill media-studio-status-pill'
  }
}

function canApproveMediaDraft(draft: ExerciseMediaDraftResponse) {
  return draft.status === 'NeedsReview' || draft.status === 'Generated'
}

function canRejectMediaDraft(draft: ExerciseMediaDraftResponse) {
  return draft.status !== 'Published'
}

function canPublishMediaDraft(draft: ExerciseMediaDraftResponse) {
  return draft.status === 'Approved'
}

function canGenerateMediaDraft(draft: ExerciseMediaDraftResponse) {
  return (
    draft.mediaType === 'Video' &&
    ['Queued', 'NeedsReview', 'Failed', 'Rejected'].includes(draft.status)
  )
}

function canRefreshMediaDraftStatus(draft: ExerciseMediaDraftResponse) {
  return draft.status === 'Generating' && Boolean(draft.providerJobId)
}

function hasGeneratedMedia(draft: ExerciseMediaDraftResponse) {
  return Boolean(draft.generatedVideoUrl || draft.generatedThumbnailUrl)
}

type MediaStudioDraftActionsProps = {
  draft: ExerciseMediaDraftResponse
  pendingActions: PendingActionMap
  onOpenExercise?: (exerciseId: number) => void
  onOpenPrompt?: (draftId: number) => Promise<void> | void
  onCopyPrompt?: () => Promise<void> | void
  onPreviewVideo?: (draft: ExerciseMediaDraftResponse) => Promise<void> | void
  onPreviewThumbnail?: (draft: ExerciseMediaDraftResponse) => Promise<void> | void
  onGenerateDraft?: (draft: ExerciseMediaDraftResponse) => Promise<void> | void
  onRefreshStatus?: (draft: ExerciseMediaDraftResponse) => Promise<void> | void
  onApproveDraft?: (draft: ExerciseMediaDraftResponse) => Promise<void> | void
  onRejectDraft?: (draft: ExerciseMediaDraftResponse) => Promise<void> | void
  onPublishDraft?: (draft: ExerciseMediaDraftResponse) => Promise<void> | void
  onClose?: () => void
}

function MediaStudioDraftActions({
  draft,
  pendingActions,
  onOpenExercise,
  onOpenPrompt,
  onCopyPrompt,
  onPreviewVideo,
  onPreviewThumbnail,
  onGenerateDraft,
  onRefreshStatus,
  onApproveDraft,
  onRejectDraft,
  onPublishDraft,
  onClose,
}: MediaStudioDraftActionsProps) {
  const isViewingDraft = pendingActions[`media-studio:view:${draft.id}`] ?? false
  const isApprovingDraft = pendingActions[`media-studio:approve:${draft.id}`] ?? false
  const isRejectingDraft = pendingActions[`media-studio:reject:${draft.id}`] ?? false
  const isPublishingDraft = pendingActions[`media-studio:publish:${draft.id}`] ?? false
  const isGeneratingDraft = pendingActions[`media-studio:generate:${draft.id}`] ?? false
  const isRefreshingStatus = pendingActions[`media-studio:refresh-status:${draft.id}`] ?? false
  const isCopyingPrompt = pendingActions[`media-studio:copy:${draft.id}`] ?? false
  const isPreviewingVideo = pendingActions[`media-studio:preview-video:${draft.id}`] ?? false
  const isPreviewingThumbnail = pendingActions[`media-studio:preview-thumbnail:${draft.id}`] ?? false
  const isDraftActionPending =
    isApprovingDraft ||
    isRejectingDraft ||
    isPublishingDraft ||
    isGeneratingDraft ||
    isRefreshingStatus ||
    isPreviewingVideo ||
    isPreviewingThumbnail
  const showApprove = canApproveMediaDraft(draft)
  const showReject = canRejectMediaDraft(draft)
  const showPublish = canPublishMediaDraft(draft)
  const canGenerate = canGenerateMediaDraft(draft)
  const canRefreshStatus = canRefreshMediaDraftStatus(draft)
  const hasGeneratedDraftMedia = hasGeneratedMedia(draft)

  return (
    <div className="media-studio-draft-actions">
      <div className="admin-actions media-studio-draft-action-group">
        {onOpenExercise ? (
          <button type="button" className="ghost-button" onClick={() => onOpenExercise(draft.exerciseCatalogItemId)}>
            Open exercise
          </button>
        ) : null}
        {onOpenPrompt ? (
          <button
            type="button"
            className="ghost-button"
            disabled={isViewingDraft || isDraftActionPending}
            onClick={() => void onOpenPrompt(draft.id)}
          >
            {isViewingDraft ? 'Loading...' : 'View prompt'}
          </button>
        ) : null}
        {draft.generatedVideoUrl && onPreviewVideo ? (
          <button
            type="button"
            className="ghost-button"
            disabled={isDraftActionPending || isCopyingPrompt}
            onClick={() => void onPreviewVideo(draft)}
          >
            {isPreviewingVideo ? 'Loading video...' : 'Preview video'}
          </button>
        ) : null}
        {draft.generatedThumbnailUrl && onPreviewThumbnail ? (
          <button
            type="button"
            className="ghost-button"
            disabled={isDraftActionPending || isCopyingPrompt}
            onClick={() => void onPreviewThumbnail(draft)}
          >
            {isPreviewingThumbnail ? 'Loading image...' : 'Preview thumbnail'}
          </button>
        ) : null}
        {onGenerateDraft ? (
          <button
            type="button"
            className="ghost-button"
            disabled={!canGenerate || isDraftActionPending || isCopyingPrompt}
            onClick={() => void onGenerateDraft(draft)}
          >
            {isGeneratingDraft ? 'Starting...' : draft.status === 'Failed' || draft.status === 'Rejected' ? 'Regenerate' : 'Generate'}
          </button>
        ) : null}
        {canRefreshStatus && onRefreshStatus ? (
          <button
            type="button"
            className="ghost-button"
            disabled={isDraftActionPending || isCopyingPrompt}
            onClick={() => void onRefreshStatus(draft)}
          >
            {isRefreshingStatus ? 'Refreshing...' : 'Refresh status'}
          </button>
        ) : null}
        {showApprove && onApproveDraft ? (
          <button
            type="button"
            className="ghost-button"
            disabled={isDraftActionPending || isCopyingPrompt}
            onClick={() => void onApproveDraft(draft)}
          >
            {isApprovingDraft ? 'Approving...' : 'Approve'}
          </button>
        ) : null}
        {showReject && onRejectDraft ? (
          <button
            type="button"
            className="ghost-button"
            disabled={isDraftActionPending || isCopyingPrompt}
            onClick={() => void onRejectDraft(draft)}
          >
            {isRejectingDraft ? 'Rejecting...' : 'Reject'}
          </button>
        ) : null}
        {showPublish && onPublishDraft ? (
          <button
            type="button"
            className="primary-button"
            disabled={!hasGeneratedDraftMedia || isDraftActionPending || isCopyingPrompt}
            onClick={() => void onPublishDraft(draft)}
          >
            {isPublishingDraft ? 'Publishing...' : 'Publish'}
          </button>
        ) : null}
        {onCopyPrompt ? (
          <button
            type="button"
            className="primary-button"
            onClick={() => void onCopyPrompt()}
            disabled={isCopyingPrompt || isDraftActionPending}
          >
            <Copy aria-hidden="true" focusable="false" strokeWidth={1.9} />
            {isCopyingPrompt ? 'Copying...' : 'Copy prompt'}
          </button>
        ) : null}
        {onClose ? (
          <button type="button" className="ghost-button" onClick={onClose} disabled={isDraftActionPending || isCopyingPrompt}>
            Close
          </button>
        ) : null}
      </div>

      {showPublish && !hasGeneratedDraftMedia ? (
        <p className="media-studio-draft-helper">Generate or attach media before publishing.</p>
      ) : null}
    </div>
  )
}

function MediaStudioReviewSummary({ draft }: { draft: ExerciseMediaDraftResponse }) {
  const message = draft.errorMessage
    ? `Generation: ${draft.errorMessage}`
    : draft.rejectionReason
    ? `Rejected: ${draft.rejectionReason}`
    : draft.reviewNotes
      ? `Review: ${draft.reviewNotes}`
      : null

  return message ? <p className="media-studio-review-summary">{truncateText(message, 180)}</p> : null
}

function MediaStudioGenerationSummary({ draft }: { draft: ExerciseMediaDraftResponse }) {
  return (
    <div className="media-studio-generation-summary">
      <span>{formatGenerationLabel(draft)}</span>
      {draft.providerJobId ? <span className="record-hint">Job {draft.providerJobId}</span> : null}
    </div>
  )
}

type MediaStudioLinkProps = {
  label: string
  value: string | null
}

function MediaStudioLink({ label, value }: MediaStudioLinkProps) {
  return (
    <div className="media-studio-link-card">
      <span className="admin-field-hint">{label}</span>
      {value ? (
        <a href={value} target="_blank" rel="noreferrer" className="media-studio-link">
          {value}
        </a>
      ) : (
        <span className="record-hint">Not set</span>
      )}
    </div>
  )
}

type MediaStudioValueProps = {
  label: string
  value: string | null
}

function MediaStudioValue({ label, value }: MediaStudioValueProps) {
  return (
    <div className="media-studio-link-card">
      <span className="admin-field-hint">{label}</span>
      <span className={value ? 'media-studio-text-value' : 'record-hint'}>{value || 'Not set'}</span>
    </div>
  )
}

function useMediaQuery(query: string) {
  const [matches, setMatches] = useState(() => {
    if (typeof window === 'undefined') {
      return false
    }

    return window.matchMedia(query).matches
  })

  useEffect(() => {
    if (typeof window === 'undefined') {
      return
    }

    const mediaQueryList = window.matchMedia(query)
    const handleChange = () => setMatches(mediaQueryList.matches)

    handleChange()
    mediaQueryList.addEventListener('change', handleChange)

    return () => mediaQueryList.removeEventListener('change', handleChange)
  }, [query])

  return matches
}
