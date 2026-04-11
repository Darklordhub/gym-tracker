import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { NavLink, Navigate, Outlet, Route, Routes, useLocation } from 'react-router-dom'
import './App.css'
import { useAuth } from './auth/AuthContext'
import { fetchCycleSettings } from './api/cycle'
import { fetchGoals } from './api/goals'
import { fetchWorkouts } from './api/workouts'
import { DashboardPage } from './pages/DashboardPage'
import { AdminPage } from './pages/AdminPage'
import { CyclePage } from './pages/CyclePage'
import { ExerciseProgressPage } from './pages/ExerciseProgressPage'
import { LoginPage } from './pages/LoginPage'
import { ProfilePage } from './pages/ProfilePage'
import { RegisterPage } from './pages/RegisterPage'
import { WeightPage } from './pages/WeightPage'
import { WorkoutsPage } from './pages/WorkoutsPage'
import { formatDate } from './lib/format'
import { generateNotifications, type AppNotification } from './lib/notifications'

const primaryNavItems = [
  { to: '/dashboard', label: 'Dashboard' },
  { to: '/weight', label: 'Weight' },
  { to: '/workouts', label: 'Workouts' },
  { to: '/exercise-progress', label: 'Exercise Progress' },
  { to: '/cycle', label: 'Cycle' },
  { to: '/profile', label: 'Profile' },
] as const

const THEME_STORAGE_KEY = 'gym-tracker-theme'
const NOTIFICATION_READ_STORAGE_KEY = 'gym-tracker-notifications-read'

type ThemeMode = 'light' | 'dark'

function App() {
  const [theme, setTheme] = useState<ThemeMode>(() => getPreferredTheme())

  useEffect(() => {
    applyTheme(theme)
  }, [theme])

  useEffect(() => {
    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)')
    const handleChange = (event: MediaQueryListEvent) => {
      const savedTheme = window.localStorage.getItem(THEME_STORAGE_KEY)
      if (savedTheme === 'light' || savedTheme === 'dark') {
        return
      }

      setTheme(event.matches ? 'dark' : 'light')
    }

    mediaQuery.addEventListener('change', handleChange)
    return () => mediaQuery.removeEventListener('change', handleChange)
  }, [])

  function handleThemeToggle() {
    setTheme((currentTheme) => {
      const nextTheme = currentTheme === 'light' ? 'dark' : 'light'
      window.localStorage.setItem(THEME_STORAGE_KEY, nextTheme)
      return nextTheme
    })
  }

  return (
    <Routes>
      <Route path="/login" element={<PublicOnlyRoute><LoginPage /></PublicOnlyRoute>} />
      <Route path="/register" element={<PublicOnlyRoute><RegisterPage /></PublicOnlyRoute>} />
      <Route element={<ProtectedRoute />}>
        <Route path="/" element={<Navigate to="/dashboard" replace />} />
        <Route element={<AppLayout theme={theme} onToggleTheme={handleThemeToggle} />}>
          <Route path="/dashboard" element={<DashboardPage />} />
          <Route path="/weight" element={<WeightPage />} />
          <Route path="/workouts" element={<WorkoutsPage />} />
          <Route path="/exercise-progress" element={<ExerciseProgressPage />} />
          <Route path="/cycle" element={<CyclePage />} />
          <Route path="/profile" element={<ProfilePage />} />
          <Route element={<AdminRoute />}>
            <Route path="/admin" element={<AdminPage />} />
          </Route>
        </Route>
      </Route>
    </Routes>
  )
}

function ProtectedRoute() {
  const { isAuthenticated, isInitializing } = useAuth()
  const location = useLocation()

  if (isInitializing) {
    return <main className="auth-shell"><section className="auth-card"><p>Loading account...</p></section></main>
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  return <Outlet />
}

function PublicOnlyRoute({ children }: { children: ReactNode }) {
  const { isAuthenticated, isInitializing } = useAuth()

  if (isInitializing) {
    return <main className="auth-shell"><section className="auth-card"><p>Loading account...</p></section></main>
  }

  if (isAuthenticated) {
    return <Navigate to="/dashboard" replace />
  }

  return children
}

function AppLayout({
  theme,
  onToggleTheme,
}: {
  theme: ThemeMode
  onToggleTheme: () => void
}) {
  const { authState, logout } = useAuth()
  const location = useLocation()
  const accountLabel = authState?.user.displayName || authState?.user.fullName || authState?.user.email
  const isAdmin = authState?.user.role === 'Admin'
  const [isCycleEnabled, setIsCycleEnabled] = useState(false)
  const navItems = (isAdmin ? [...primaryNavItems, { to: '/admin', label: 'Admin' as const }] : primaryNavItems)
    .filter((item) => item.to !== '/cycle' || isCycleEnabled)
  const [isMobileNavOpen, setIsMobileNavOpen] = useState(false)
  const [notifications, setNotifications] = useState<AppNotification[]>([])
  const [readNotificationIds, setReadNotificationIds] = useState<string[]>(() => getStoredReadNotificationIds())
  const [isNotificationsOpen, setIsNotificationsOpen] = useState(false)
  const notificationCenterRef = useRef<HTMLDivElement | null>(null)

  useEffect(() => {
    setIsMobileNavOpen(false)
    setIsNotificationsOpen(false)
  }, [location.pathname])

  useEffect(() => {
    void loadNotifications()
  }, [location.pathname])

  useEffect(() => {
    void loadCycleVisibility()
  }, [])

  useEffect(() => {
    function handlePointerDown(event: MouseEvent) {
      if (!notificationCenterRef.current?.contains(event.target as Node)) {
        setIsNotificationsOpen(false)
      }
    }

    document.addEventListener('mousedown', handlePointerDown)
    return () => document.removeEventListener('mousedown', handlePointerDown)
  }, [])

  useEffect(() => {
    function handleCycleSettingsUpdated(event: Event) {
      const customEvent = event as CustomEvent<{ isEnabled?: boolean }>
      setIsCycleEnabled(Boolean(customEvent.detail?.isEnabled))
    }

    window.addEventListener('cycle-settings-updated', handleCycleSettingsUpdated as EventListener)
    return () => window.removeEventListener('cycle-settings-updated', handleCycleSettingsUpdated as EventListener)
  }, [])

  useEffect(() => {
    const notificationIds = new Set(notifications.map((notification) => notification.id))
    setReadNotificationIds((current) => {
      const next = current.filter((id) => notificationIds.has(id))
      if (next.length !== current.length) {
        window.localStorage.setItem(NOTIFICATION_READ_STORAGE_KEY, JSON.stringify(next))
      }
      return next
    })
  }, [notifications])

  const unreadCount = useMemo(
    () => notifications.filter((notification) => !readNotificationIds.includes(notification.id)).length,
    [notifications, readNotificationIds],
  )

  async function loadNotifications() {
    try {
      const [workoutData, goalData] = await Promise.all([fetchWorkouts(), fetchGoals().catch(() => null)])
      setNotifications(generateNotifications(workoutData, goalData))
    } catch {
      setNotifications([])
    }
  }

  async function loadCycleVisibility() {
    try {
      const settings = await fetchCycleSettings()
      setIsCycleEnabled(settings.isEnabled)
    } catch {
      setIsCycleEnabled(false)
    }
  }

  function markNotificationAsRead(notificationId: string) {
    setReadNotificationIds((current) => {
      if (current.includes(notificationId)) {
        return current
      }

      const next = [...current, notificationId]
      window.localStorage.setItem(NOTIFICATION_READ_STORAGE_KEY, JSON.stringify(next))
      return next
    })
  }

  function markAllNotificationsAsRead() {
    const allIds = notifications.map((notification) => notification.id)
    setReadNotificationIds(allIds)
    window.localStorage.setItem(NOTIFICATION_READ_STORAGE_KEY, JSON.stringify(allIds))
  }

  return (
    <div className="app-shell">
      <header className="app-header">
        <div className="app-header-top">
          <div className="brand-block">
            <p className="brand-kicker">Training log</p>
            <p className="brand-name">Gym Tracker</p>
            <p className="brand-subtitle">Weight, workouts, progress, and planning in one place.</p>
          </div>

          <div className="header-utility-row">
            <NotificationCenter
              containerRef={notificationCenterRef}
              notifications={notifications}
              unreadCount={unreadCount}
              isOpen={isNotificationsOpen}
              onToggle={() => setIsNotificationsOpen((current) => !current)}
              onMarkRead={markNotificationAsRead}
              onMarkAllRead={markAllNotificationsAsRead}
              readNotificationIds={readNotificationIds}
            />

            <div className="mobile-topbar-actions">
              <button
                type="button"
                className="ghost-button mobile-nav-toggle"
                aria-expanded={isMobileNavOpen}
                aria-controls="primary-navigation"
                onClick={() => setIsMobileNavOpen((current) => !current)}
              >
                {isMobileNavOpen ? 'Close menu' : 'Menu'}
              </button>
            </div>

            <div className="account-chip">
              <button
                type="button"
                className="ghost-button theme-toggle-button"
                onClick={onToggleTheme}
                aria-label={`Switch to ${theme === 'light' ? 'dark' : 'light'} mode`}
                title={`Switch to ${theme === 'light' ? 'dark' : 'light'} mode`}
              >
                {theme === 'light' ? 'Dark mode' : 'Light mode'}
              </button>
              <div className="account-copy">
                <span title={authState?.user.email}>{accountLabel}</span>
                <small>{authState?.user.role ?? 'User'} account</small>
              </div>
              <button type="button" className="ghost-button" onClick={logout}>
                Log out
              </button>
            </div>
          </div>
        </div>

        <div className="header-actions">
          <nav className="main-nav-shell" aria-label="Primary">
            <div
              id="primary-navigation"
              className={isMobileNavOpen ? 'main-nav main-nav-mobile-open' : 'main-nav'}
            >
              {navItems.map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  className={({ isActive }) => (isActive ? 'nav-link nav-link-active' : 'nav-link')}
                >
                  {item.label}
                </NavLink>
              ))}

              <div className="mobile-menu-secondary">
                <div className="mobile-account-copy">
                  <span title={authState?.user.email}>{accountLabel}</span>
                  <small>{authState?.user.role ?? 'User'} account</small>
                </div>
                <div className="mobile-menu-actions">
                  <button
                    type="button"
                    className="ghost-button mobile-menu-action"
                    onClick={onToggleTheme}
                    aria-label={`Switch to ${theme === 'light' ? 'dark' : 'light'} mode`}
                    title={`Switch to ${theme === 'light' ? 'dark' : 'light'} mode`}
                  >
                    {theme === 'light' ? 'Dark mode' : 'Light mode'}
                  </button>
                  <button type="button" className="ghost-button mobile-menu-action" onClick={logout}>
                    Log out
                  </button>
                </div>
              </div>
            </div>
          </nav>
        </div>
      </header>

      <div className="app-content">
        <Outlet />
      </div>
    </div>
  )
}

function NotificationCenter({
  containerRef,
  notifications,
  unreadCount,
  isOpen,
  onToggle,
  onMarkRead,
  onMarkAllRead,
  readNotificationIds,
}: {
  containerRef: React.RefObject<HTMLDivElement | null>
  notifications: AppNotification[]
  unreadCount: number
  isOpen: boolean
  onToggle: () => void
  onMarkRead: (notificationId: string) => void
  onMarkAllRead: () => void
  readNotificationIds: string[]
}) {
  return (
    <div className="notification-center" ref={containerRef}>
      <button
        type="button"
        className={isOpen ? 'ghost-button notification-toggle notification-toggle-open' : 'ghost-button notification-toggle'}
        aria-expanded={isOpen}
        aria-controls="notification-panel"
        aria-label={`Notifications${unreadCount > 0 ? `, ${unreadCount} unread` : ''}`}
        onClick={onToggle}
      >
        <span className="notification-bell" aria-hidden="true">
          <svg viewBox="0 0 24 24" focusable="false">
            <path
              d="M12 3a4 4 0 0 0-4 4v1.1a7 7 0 0 1-1.6 4.5L5 14.3V16h14v-1.7l-1.4-1.7A7 7 0 0 1 16 8.1V7a4 4 0 0 0-4-4Z"
              fill="none"
              stroke="currentColor"
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth="1.8"
            />
            <path
              d="M10 18a2 2 0 0 0 4 0"
              fill="none"
              stroke="currentColor"
              strokeLinecap="round"
              strokeWidth="1.8"
            />
          </svg>
        </span>
        <span className="notification-toggle-label">Alerts</span>
        {unreadCount > 0 ? <span className="notification-indicator">{unreadCount}</span> : null}
      </button>

      {isOpen ? (
        <div id="notification-panel" className="notification-panel" role="dialog" aria-label="Notifications">
          <div className="notification-panel-header">
            <div>
              <strong>Notifications</strong>
              <p>Generated from your recent workouts, goals, and progress signals.</p>
            </div>
            {notifications.length > 0 ? (
              <button type="button" className="ghost-button compact-button" onClick={onMarkAllRead}>
                Mark all read
              </button>
            ) : null}
          </div>

          {notifications.length === 0 ? (
            <div className="notification-empty">
              <strong>No notifications</strong>
              <p>Reminders and achievements will show up here when your data suggests them.</p>
            </div>
          ) : (
            <div className="notification-list">
              {notifications.map((notification) => {
                const isRead = readNotificationIds.includes(notification.id)

                return (
                  <article
                    key={notification.id}
                    className={isRead ? 'notification-card notification-card-read' : 'notification-card'}
                  >
                    <div className="notification-card-header">
                      <div className="notification-card-copy">
                        <span className="stat-label">{formatNotificationType(notification.type)}</span>
                        <strong>{notification.title}</strong>
                      </div>
                      {!isRead ? <span className="notification-dot" aria-hidden="true" /> : null}
                    </div>
                    <p>{notification.message}</p>
                    <div className="notification-card-footer">
                      <span className="record-hint">
                        {formatDate(notification.createdAt)}
                      </span>
                      <button
                        type="button"
                        className="ghost-button compact-button"
                        onClick={() => onMarkRead(notification.id)}
                        disabled={isRead}
                      >
                        {isRead ? 'Read' : 'Mark read'}
                      </button>
                    </div>
                  </article>
                )
              })}
            </div>
          )}
        </div>
      ) : null}
    </div>
  )
}

function AdminRoute() {
  const { authState } = useAuth()

  if (authState?.user.role !== 'Admin') {
    return <Navigate to="/dashboard" replace />
  }

  return <Outlet />
}

function getPreferredTheme(): ThemeMode {
  if (typeof window === 'undefined') {
    return 'light'
  }

  const savedTheme = window.localStorage.getItem(THEME_STORAGE_KEY)
  if (savedTheme === 'light' || savedTheme === 'dark') {
    return savedTheme
  }

  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

function applyTheme(theme: ThemeMode) {
  document.documentElement.dataset.theme = theme
  document.documentElement.style.colorScheme = theme
}

function getStoredReadNotificationIds() {
  if (typeof window === 'undefined') {
    return []
  }

  try {
    const value = window.localStorage.getItem(NOTIFICATION_READ_STORAGE_KEY)
    if (!value) {
      return []
    }

    const parsed = JSON.parse(value)
    return Array.isArray(parsed) ? parsed.filter((entry): entry is string => typeof entry === 'string') : []
  } catch {
    return []
  }
}

function formatNotificationType(type: AppNotification['type']) {
  switch (type) {
    case 'weekly-goal-reminder':
      return 'Weekly reminder'
    case 'inactivity-reminder':
      return 'Inactivity'
    case 'pr-opportunity':
      return 'PR opportunity'
    case 'goal-achievement':
      return 'Achievement'
  }
}

export default App
