import { useDeferredValue, useEffect, useState } from 'react'
import {
  fetchAdminExerciseCatalog,
  fetchAdminUsers,
  resetAdminExerciseCatalogItem,
  resetAdminUserPassword,
  syncAdminExerciseCatalogFromWger,
  updateAdminExerciseCatalogItem,
  updateAdminUserRole,
  updateAdminUserStatus,
} from '../api/admin'
import { StateCard } from '../components/StateCard'
import { useAuth } from '../auth/AuthContext'
import { formatDate } from '../lib/format'
import { getRequestErrorMessage, isForbiddenError } from '../lib/http'
import type {
  AdminExerciseCatalogItem,
  AdminUser,
  UpdateExerciseCatalogItemPayload,
} from '../types/admin'

type PendingActionMap = Record<string, boolean>

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
  const [isLoadingUsers, setIsLoadingUsers] = useState(true)
  const [isLoadingCatalog, setIsLoadingCatalog] = useState(true)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)
  const [pendingActions, setPendingActions] = useState<PendingActionMap>({})
  const [resetPasswordUser, setResetPasswordUser] = useState<AdminUser | null>(null)
  const [resetPasswordForm, setResetPasswordForm] = useState({ newPassword: '', confirmPassword: '' })
  const [resetPasswordError, setResetPasswordError] = useState<string | null>(null)
  const [catalogSearch, setCatalogSearch] = useState('')
  const [editingCatalogItem, setEditingCatalogItem] = useState<AdminExerciseCatalogItem | null>(null)
  const [catalogForm, setCatalogForm] = useState<UpdateExerciseCatalogItemPayload>(initialCatalogFormState)
  const [catalogFormError, setCatalogFormError] = useState<string | null>(null)
  const deferredCatalogSearch = useDeferredValue(catalogSearch)

  useEffect(() => {
    void loadUsers()
  }, [])

  useEffect(() => {
    void loadCatalog(deferredCatalogSearch)
  }, [deferredCatalogSearch])

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

  return (
    <main className="page-shell">
      <section className="hero-panel">
        <div className="hero-copy">
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
            <div className="admin-table-shell">
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
                        <td className="admin-cell-strong">
                          <div className="admin-user-cell">
                            <strong>{user.email}</strong>
                            <span className="record-hint">Created {formatDate(user.createdAt)}</span>
                          </div>
                        </td>
                        <td>{user.fullName || 'Not set'}</td>
                        <td>{user.displayName || 'Not set'}</td>
                        <td>
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
                        <td>
                          <span className={user.isActive ? 'status-pill status-pill-active' : 'status-pill status-pill-inactive'}>
                            {user.isActive ? 'Active' : 'Inactive'}
                          </span>
                        </td>
                        <td className="record-hint">{formatDate(user.createdAt)}</td>
                        <td>
                          <div className="admin-actions">
                            <button type="button" className="ghost-button" disabled={isUpdating} onClick={() => openResetPasswordDialog(user)}>
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
            <div className="admin-table-shell">
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
                  {catalogItems.map((item) => {
                    const isStatusUpdating = pendingActions[`catalog:status:${item.id}`] ?? false

                    return (
                      <tr key={item.id} className={isStatusUpdating ? 'admin-row admin-row-updating' : 'admin-row'}>
                        <td className="admin-cell-strong">
                          <div className="admin-user-cell">
                            <strong>{item.name}</strong>
                            <span className="record-hint">
                              {item.isManuallyEdited ? 'Local override active' : `Provider: ${item.providerName}`}
                            </span>
                          </div>
                        </td>
                        <td>{item.source}</td>
                        <td>{item.primaryMuscle ? formatCatalogLabel(item.primaryMuscle) : 'Not set'}</td>
                        <td>{item.equipment ? formatCatalogLabel(item.equipment) : 'Not set'}</td>
                        <td>
                          <span className={item.isActive ? 'status-pill status-pill-active' : 'status-pill status-pill-inactive'}>
                            {item.isActive ? 'Active' : 'Inactive'}
                          </span>
                        </td>
                        <td className="record-hint">{item.lastSyncedAt ? formatDate(item.lastSyncedAt) : 'Never'}</td>
                        <td>
                          <div className="admin-actions">
                            <button type="button" className="ghost-button" onClick={() => openCatalogEditor(item)}>
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
            </div>
          )}
        </div>
      </section>

      {resetPasswordUser ? (
        <div className="modal-backdrop" role="presentation" onClick={closeResetPasswordDialog}>
          <div className="modal-panel" role="dialog" aria-modal="true" aria-labelledby="reset-password-title" onClick={(event) => event.stopPropagation()}>
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
          <div className="modal-panel admin-catalog-modal" role="dialog" aria-modal="true" aria-labelledby="catalog-editor-title" onClick={(event) => event.stopPropagation()}>
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
