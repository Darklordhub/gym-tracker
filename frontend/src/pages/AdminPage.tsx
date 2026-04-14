import { useEffect, useState } from 'react'
import {
  fetchAdminUsers,
  resetAdminUserPassword,
  updateAdminUserRole,
  updateAdminUserStatus,
} from '../api/admin'
import { StateCard } from '../components/StateCard'
import { useAuth } from '../auth/AuthContext'
import { formatDate } from '../lib/format'
import { getRequestErrorMessage, isForbiddenError } from '../lib/http'
import type { AdminUser } from '../types/admin'

type PendingActionMap = Record<string, boolean>

export function AdminPage() {
  const { authState } = useAuth()
  const [users, setUsers] = useState<AdminUser[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)
  const [pendingActions, setPendingActions] = useState<PendingActionMap>({})
  const [resetPasswordUser, setResetPasswordUser] = useState<AdminUser | null>(null)
  const [resetPasswordForm, setResetPasswordForm] = useState({ newPassword: '', confirmPassword: '' })
  const [resetPasswordError, setResetPasswordError] = useState<string | null>(null)

  useEffect(() => {
    void loadUsers()
  }, [])

  async function loadUsers() {
    try {
      setIsLoading(true)
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
      setIsLoading(false)
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

  return (
    <main className="page-shell">
      <section className="hero-panel">
        <div className="hero-copy">
          <span className="eyebrow">Admin</span>
          <h1>User Management</h1>
          <p className="hero-text">
            Review user accounts, confirm access levels, and activate or deactivate access from one place.
          </p>
        </div>

        <div className="stats-grid">
          <article className="stat-card">
            <span className="stat-label">Total Users</span>
            <strong>{users.length}</strong>
            <span className="stat-subtext">Accounts currently stored in the app database.</span>
          </article>
          <article className="stat-card">
            <span className="stat-label">Active Users</span>
            <strong>{users.filter((user) => user.isActive).length}</strong>
            <span className="stat-subtext">Accounts that can still log in.</span>
          </article>
          <article className="stat-card">
            <span className="stat-label">Admins</span>
            <strong>{users.filter((user) => user.role === 'Admin').length}</strong>
            <span className="stat-subtext">Accounts with admin access.</span>
          </article>
          <article className="stat-card">
            <span className="stat-label">Current Session</span>
            <strong>{authState?.user.role ?? 'Unknown'}</strong>
            <span className="stat-subtext">Your own access level for this session.</span>
          </article>
        </div>
      </section>

      <section className="content-grid admin-grid">
        <div className="panel panel-span-2">
          <div className="panel-header">
            <div>
              <h2>Users</h2>
              <p>Manage account role and access status while keeping the current admin safeguards intact.</p>
            </div>
          </div>

          {successMessage || errorMessage ? (
            <div className="feedback-stack">
              {successMessage ? <p className="feedback success">{successMessage}</p> : null}
              {errorMessage ? <p className="feedback error">{errorMessage}</p> : null}
            </div>
          ) : null}

          {isLoading ? (
            <StateCard title="Loading users" description="Fetching current user accounts." loading />
          ) : users.length === 0 ? (
            <StateCard
              title="No users found"
              description="User accounts will appear here once people start registering."
            />
          ) : (
            <div className="admin-table-shell">
              <table className="admin-table">
                <caption className="sr-only">
                  User administration table with role selection and account status controls.
                </caption>
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
                              onChange={(event) =>
                                void handleRoleChange(user.id, event.target.value as AdminUser['role'])
                              }
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
                            <button
                              type="button"
                              className="ghost-button"
                              disabled={isUpdating}
                              onClick={() => openResetPasswordDialog(user)}
                            >
                              {isPasswordResetting ? 'Saving...' : 'Reset Password'}
                            </button>
                            <button
                              type="button"
                              className={user.isActive ? 'ghost-button subtle-danger-button' : 'ghost-button'}
                              disabled={isUpdating}
                              onClick={() => void handleStatusToggle(user.id, !user.isActive)}
                            >
                              {isStatusUpdating
                                ? 'Saving...'
                                : user.isActive
                                  ? 'Deactivate'
                                  : 'Activate'}
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
                  onChange={(event) =>
                    setResetPasswordForm((current) => ({ ...current, newPassword: event.target.value }))
                  }
                  placeholder="At least 8 characters"
                />
              </label>

              <label className="field">
                <span>Confirm password</span>
                <input
                  type="password"
                  minLength={8}
                  value={resetPasswordForm.confirmPassword}
                  onChange={(event) =>
                    setResetPasswordForm((current) => ({ ...current, confirmPassword: event.target.value }))
                  }
                  placeholder="Re-enter the new password"
                />
              </label>

              {resetPasswordError ? <p className="feedback error">{resetPasswordError}</p> : null}

              <div className="action-row">
                <button type="button" className="ghost-button" onClick={closeResetPasswordDialog}>
                  Cancel
                </button>
                <button
                  type="submit"
                  className="primary-button"
                  disabled={pendingActions[`password:${resetPasswordUser.id}`] ?? false}
                >
                  {pendingActions[`password:${resetPasswordUser.id}`] ?? false ? 'Resetting...' : 'Reset password'}
                </button>
              </div>
            </form>
          </div>
        </div>
      ) : null}
    </main>
  )
}
