import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { NavLink, Navigate, Outlet, Route, Routes, useLocation } from 'react-router-dom'
import type { LucideIcon } from 'lucide-react'
import {
  Activity,
  Bell,
  BookOpen,
  Dumbbell,
  LayoutDashboard,
  LogOut,
  Menu,
  Moon,
  Scale,
  Shield,
  SunMedium,
  TrendingUp,
  UserRound,
} from 'lucide-react'
import './App.css'
import { useAuth } from './auth/AuthContext'
import { fetchCycleSettings } from './api/cycle'
import { fetchGoals } from './api/goals'
import { fetchWorkouts } from './api/workouts'
import { DashboardPage } from './pages/DashboardPage'
import { AdminPage } from './pages/AdminPage'
import { CyclePage } from './pages/CyclePage'
import { ExerciseLibraryPage } from './pages/ExerciseLibraryPage'
import { ExerciseProgressPage } from './pages/ExerciseProgressPage'
import { LoginPage } from './pages/LoginPage'
import { ProfilePage } from './pages/ProfilePage'
import { RegisterPage } from './pages/RegisterPage'
import { WeightPage } from './pages/WeightPage'
import { WorkoutsPage } from './pages/WorkoutsPage'
import { formatDate } from './lib/format'
import { generateNotifications, type AppNotification } from './lib/notifications'

type ThemeMode = 'light' | 'dark'
type IconName =
  | 'dashboard'
  | 'weight'
  | 'workouts'
  | 'progress'
  | 'cycle'
  | 'profile'
  | 'admin'
  | 'library'
  | 'moon'
  | 'sun'
  | 'logout'

type NavItem = {
  to: string
  label: string
  icon: IconName
}

const primaryNavItems: readonly NavItem[] = [
  { to: '/dashboard', label: 'Dashboard', icon: 'dashboard' },
  { to: '/weight', label: 'Weight', icon: 'weight' },
  { to: '/workouts', label: 'Workouts', icon: 'workouts' },
  { to: '/exercise-library', label: 'Exercise Library', icon: 'library' },
  { to: '/exercise-progress', label: 'Exercise Progress', icon: 'progress' },
  { to: '/cycle', label: 'Cycle', icon: 'cycle' },
  { to: '/profile', label: 'Profile', icon: 'profile' },
] as const

const routeMeta: Record<string, { title: string; eyebrow: string; description: string }> = {
  '/dashboard': {
    title: 'Command Center',
    eyebrow: 'Overview',
    description: 'Training, body metrics, and recovery signals in one operating view.',
  },
  '/weight': {
    title: 'Bodyweight Log',
    eyebrow: 'Composition',
    description: 'Review logged entries, trends, and progress signals without leaving the shell.',
  },
  '/workouts': {
    title: 'Session Library',
    eyebrow: 'Workload',
    description: 'Track strength and cardio sessions with a layout tuned for dense training data.',
  },
  '/exercise-library': {
    title: 'Exercise Library',
    eyebrow: 'Catalog',
    description: 'Review the local exercise catalog foundation that future provider sync will build on.',
  },
  '/exercise-progress': {
    title: 'Performance Trends',
    eyebrow: 'Progress',
    description: 'Compare exercise history, records, and movement-specific momentum over time.',
  },
  '/cycle': {
    title: 'Cycle Intelligence',
    eyebrow: 'Readiness',
    description: 'View phase context, prediction signals, and planning inputs in the same system.',
  },
  '/profile': {
    title: 'Athlete Profile',
    eyebrow: 'Account',
    description: 'Maintain account information and preferences without breaking the training flow.',
  },
  '/admin': {
    title: 'Control Room',
    eyebrow: 'Admin',
    description: 'Manage privileged settings and operational controls from a protected surface.',
  },
}

const THEME_STORAGE_KEY = 'gym-tracker-theme'
const NOTIFICATION_READ_STORAGE_KEY = 'gym-tracker-notifications-read'
const APP_BRAND_NAME = 'GYM Tracker'
const APP_BRAND_SHORT = 'GYM Tracker'

function App() {
  const [theme, setTheme] = useState<ThemeMode>(() => getPreferredTheme())

  useEffect(() => {
    applyTheme(theme)
  }, [theme])

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
          <Route path="/exercise-library" element={<ExerciseLibraryPage />} />
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
  const navItems = (isAdmin
    ? [...primaryNavItems, { to: '/admin', label: 'Admin', icon: 'admin' as const }]
    : primaryNavItems).filter((item) => item.to !== '/cycle' || isCycleEnabled)
  const [isMobileNavOpen, setIsMobileNavOpen] = useState(false)
  const [notifications, setNotifications] = useState<AppNotification[]>([])
  const [readNotificationIds, setReadNotificationIds] = useState<string[]>(() => getStoredReadNotificationIds())
  const [isNotificationsOpen, setIsNotificationsOpen] = useState(false)
  const notificationCenterRef = useRef<HTMLDivElement | null>(null)
  const topbarMeta = routeMeta[location.pathname] ?? {
    title: APP_BRAND_NAME,
    eyebrow: 'Navigation',
    description: 'Shared shell, theme, and layout foundation for the training workspace.',
  }
  const themeToggleLabel = `Switch to ${theme === 'light' ? 'dark' : 'light'} mode`
  const themeButtonLabel = theme === 'light' ? 'Dark mode' : 'Light mode'

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

  useEffect(() => {
    if (!isMobileNavOpen) {
      document.body.classList.remove('nav-open')
      return
    }

    document.body.classList.add('nav-open')
    return () => document.body.classList.remove('nav-open')
  }, [isMobileNavOpen])

  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setIsMobileNavOpen(false)
        setIsNotificationsOpen(false)
      }
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [])

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
    <div className="forge-shell">
      <div className={isMobileNavOpen ? 'shell-backdrop shell-backdrop-visible' : 'shell-backdrop'} onClick={() => setIsMobileNavOpen(false)} aria-hidden="true" />

      <aside
        id="primary-navigation"
        className={isMobileNavOpen ? 'app-sidebar app-sidebar-open' : 'app-sidebar'}
      >
        <div className="sidebar-inner">
          <div className="sidebar-brand">
            <div className="brand-mark" aria-hidden="true">
              <span />
            </div>
            <div className="sidebar-brand-copy">
              <span className="brand-kicker">FORGE</span>
              <strong className="brand-name">{APP_BRAND_NAME}</strong>
              <p className="brand-subtitle">Track training, recovery, and progress in one controlled workspace.</p>
            </div>
          </div>

          <div className="sidebar-section">
            <div className="sidebar-section-header">
              <span>Navigation</span>
              <span className="sidebar-section-count">{navItems.length}</span>
            </div>
            <nav className="sidebar-nav" aria-label="Primary">
              {navItems.map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  title={item.label}
                  className={({ isActive }) => (isActive ? 'sidebar-link sidebar-link-active' : 'sidebar-link')}
                >
                  <span className="sidebar-link-icon" aria-hidden="true">
                    <AppIcon name={item.icon} />
                  </span>
                  <span className="sidebar-link-copy">
                    <span className="sidebar-link-label">{item.label}</span>
                    <span className="sidebar-link-meta">{getNavMeta(item.to)}</span>
                  </span>
                </NavLink>
              ))}
            </nav>
          </div>

          <div className="sidebar-section sidebar-section-secondary">
            <div className="sidebar-section-header">
              <span>Account</span>
              <span className="status-pill status-pill-lime">{authState?.user.role ?? 'User'}</span>
            </div>
            <div className="account-panel">
              <div className="user-pill">
                <div className="avatar-ring" aria-hidden="true">
                  <div className="avatar-inner">{getInitials(accountLabel)}</div>
                </div>
                <div className="user-copy">
                  <strong title={authState?.user.email}>{accountLabel}</strong>
                  <span>{authState?.user.role ?? 'User'} access</span>
                </div>
              </div>

              <div className="sidebar-actions">
                <button
                  type="button"
                  className="ghost-button sidebar-action-button"
                  onClick={onToggleTheme}
                  aria-label={themeToggleLabel}
                  title={themeToggleLabel}
                >
                  <AppIcon name={theme === 'light' ? 'moon' : 'sun'} />
                  <span className="sidebar-action-label">{themeButtonLabel}</span>
                </button>
                <button
                  type="button"
                  className="ghost-button sidebar-action-button"
                  onClick={logout}
                  aria-label="Log out"
                  title="Log out"
                >
                  <AppIcon name="logout" />
                  <span className="sidebar-action-label">Log out</span>
                </button>
              </div>
            </div>
          </div>
        </div>
      </aside>

      <div className="app-main">
        <header className="app-topbar">
          <div className="topbar-left">
            <button
              type="button"
              className="topbar-icon-button mobile-nav-toggle"
              aria-expanded={isMobileNavOpen}
              aria-controls="primary-navigation"
              onClick={() => setIsMobileNavOpen((current) => !current)}
            >
              <HamburgerIcon />
              <span className="sr-only">{isMobileNavOpen ? 'Close navigation menu' : 'Open navigation menu'}</span>
            </button>

            <div className="topbar-breadcrumb">
              <span>{APP_BRAND_SHORT}</span>
              <span className="sep">/</span>
              <span>{topbarMeta.eyebrow}</span>
              <span className="sep">/</span>
              <span className="current">{topbarMeta.title}</span>
            </div>

            <div className="topbar-title-block">
              <span className="topbar-kicker">{topbarMeta.eyebrow}</span>
              <strong>{topbarMeta.title}</strong>
              <p>{topbarMeta.description}</p>
            </div>
          </div>

          <div className="tb-right">
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

            <button
              type="button"
              className="topbar-icon-button"
              onClick={onToggleTheme}
              aria-label={themeToggleLabel}
              title={themeToggleLabel}
            >
              <ThemeIcon theme={theme} />
            </button>

            <div className="topbar-user">
              <div className="avatar-ring" aria-hidden="true">
                <div className="avatar-inner">{getInitials(accountLabel)}</div>
              </div>
              <div className="topbar-user-copy">
                <strong title={authState?.user.email}>{accountLabel}</strong>
                <span>{authState?.user.role ?? 'User'} account</span>
              </div>
            </div>
          </div>
        </header>

        <main className="app-content">
          <div className="content-container">
            <Outlet />
          </div>
        </main>
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
        className={isOpen ? 'topbar-icon-button notification-toggle notification-toggle-open' : 'topbar-icon-button notification-toggle'}
        aria-expanded={isOpen}
        aria-controls="notification-panel"
        aria-label={`Notifications${unreadCount > 0 ? `, ${unreadCount} unread` : ''}`}
        onClick={onToggle}
      >
        <span className="notification-bell" aria-hidden="true"><BellIcon /></span>
        {unreadCount > 0 ? <span className="notification-indicator">{unreadCount}</span> : null}
      </button>

      {isOpen ? (
        <div id="notification-panel" className="notification-panel" role="dialog" aria-label="Notifications">
          <div className="notification-panel-header">
            <div>
              <strong>Notifications</strong>
              <p>Generated from recent workouts, goals, and progression signals.</p>
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
              <p>Reminders and achievements will surface here when your data suggests them.</p>
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
                      <span className="record-hint">{formatDate(notification.createdAt)}</span>
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
    return 'dark'
  }

  const savedTheme = window.localStorage.getItem(THEME_STORAGE_KEY)
  if (savedTheme === 'light' || savedTheme === 'dark') {
    return savedTheme
  }

  return 'dark'
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

function getNavMeta(pathname: string) {
  switch (pathname) {
    case '/dashboard':
      return 'Overview'
    case '/weight':
      return 'Body metrics'
    case '/workouts':
      return 'Session logs'
    case '/exercise-library':
      return 'Catalog'
    case '/exercise-progress':
      return 'Lift history'
    case '/cycle':
      return 'Readiness'
    case '/profile':
      return 'Account'
    case '/admin':
      return 'Controls'
    default:
      return 'Workspace'
  }
}

function getInitials(value?: string) {
  if (!value) {
    return 'GT'
  }

  const parts = value
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((entry) => entry[0]?.toUpperCase() ?? '')
    .join('')

  return parts || value.slice(0, 2).toUpperCase()
}

function AppIcon({ name }: { name: IconName }) {
  const iconMap: Record<IconName, LucideIcon> = {
    dashboard: LayoutDashboard,
    weight: Scale,
    workouts: Dumbbell,
    progress: TrendingUp,
    library: BookOpen,
    cycle: Activity,
    profile: UserRound,
    admin: Shield,
    moon: Moon,
    sun: SunMedium,
    logout: LogOut,
  }

  const Icon = iconMap[name]
  return <Icon aria-hidden="true" focusable="false" strokeWidth={1.9} />
}

function ThemeIcon({ theme }: { theme: ThemeMode }) {
  return <AppIcon name={theme === 'light' ? 'moon' : 'sun'} />
}

function HamburgerIcon() {
  return <Menu aria-hidden="true" focusable="false" strokeWidth={1.9} />
}

function BellIcon() {
  return <Bell aria-hidden="true" focusable="false" strokeWidth={1.9} />
}

export default App
