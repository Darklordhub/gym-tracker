import { useEffect, useState } from 'react'
import { fetchAdminUsers, updateAdminUserRole, updateAdminUserStatus } from '../api/admin'
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

  return (
    <main className="page-shell">
      <section className="hero-panel">
        <div className="hero-copy">
          <span className="eyebrow">Admin</span>
          <h1>User Management</h1>
          <p className="hero-text">
            Review user accounts, manage roles, and activate or deactivate access.
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
              <p>Basic user administration with role and account status controls.</p>
            </div>
          </div>

          {successMessage ? <p className="feedback success">{successMessage}</p> : null}
          {errorMessage ? <p className="feedback error">{errorMessage}</p> : null}

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
                    const isUpdating = isRoleUpdating || isStatusUpdating

                    return (
                      <tr key={user.id}>
                        <td className="admin-cell-strong">{user.email}</td>
                        <td>{user.fullName || 'Not set'}</td>
                        <td>{user.displayName || 'Not set'}</td>
                        <td>
                          <label className="admin-select-label">
                            <span className="sr-only">Role for {user.email}</span>
                            <select
                              className="select-input"
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
                        <td>{formatDate(user.createdAt)}</td>
                        <td>
                          <button
                            type="button"
                            className={user.isActive ? 'danger-button' : 'ghost-button'}
                            disabled={isUpdating}
                            onClick={() => void handleStatusToggle(user.id, !user.isActive)}
                          >
                            {isStatusUpdating
                              ? 'Saving...'
                              : user.isActive
                                ? 'Deactivate'
                                : 'Activate'}
                          </button>
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
    </main>
  )
}
