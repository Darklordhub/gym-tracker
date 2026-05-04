import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { Navigate, Outlet, Route, Routes, useLocation } from 'react-router-dom'
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
  Sparkles,
  Shield,
  SunMedium,
  TrendingUp,
  UtensilsCrossed,
  UserRound,
} from 'lucide-react'
import './App.css'
import { useAuth } from './auth/useAuth'
import { fetchCycleSettings } from './api/cycle'
import { fetchGoals } from './api/goals'
import { fetchWorkouts } from './api/workouts'
import { DashboardPage } from './pages/DashboardPage'
import { AdminPage } from './pages/AdminPage'
import { CyclePage } from './pages/CyclePage'
import { AiWorkoutGeneratorPage } from './pages/AiWorkoutGeneratorPage'
import { ExerciseLibraryPage } from './pages/ExerciseLibraryPage'
import { ExerciseProgressPage } from './pages/ExerciseProgressPage'
import { LoginPage } from './pages/LoginPage'
import { NutritionPage } from './pages/NutritionPage'
import { ProfilePage } from './pages/ProfilePage'
import { RegisterPage } from './pages/RegisterPage'
import { WeightPage } from './pages/WeightPage'
import { WorkoutsPage } from './pages/WorkoutsPage'
import { generateNotifications, type AppNotification } from './lib/notifications'
import { StrideShell } from './components/layout/StrideShell'
import { StrideSidebar } from './components/layout/StrideSidebar'
import { StrideTopbar } from './components/layout/StrideTopbar'
import type {
  IconName,
  StrideSidebarNavItem,
  StrideTopbarMeta,
  ThemeMode,
} from './components/layout/strideLayoutTypes'
import {
  adminNavItems,
  getNavMeta,
  primaryNavItems,
} from './components/layout/strideNavigation'

const routeMeta: Record<string, StrideTopbarMeta> = {
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
  '/ai-workout-generator': {
    title: 'AI Workout Generator',
    eyebrow: 'Generator',
    description: 'Create a read-only workout blueprint from your goals, recent logs, and the current exercise catalog.',
  },
  '/nutrition': {
    title: 'Nutrition Builder',
    eyebrow: 'Nutrition',
    description: 'Search USDA foods, manage meals by day, and sync meal-managed totals into the daily calorie workspace.',
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
          <Route path="/ai-workout-generator" element={<AiWorkoutGeneratorPage />} />
          <Route path="/nutrition" element={<NutritionPage />} />
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
  const accountRole = authState?.user.role ?? 'User'
  const accountInitials = getInitials(accountLabel)
  const isAdmin = authState?.user.role === 'Admin'
  const [isCycleEnabled, setIsCycleEnabled] = useState(false)
  const visiblePrimaryNavItems = primaryNavItems.filter((item) => item.to !== '/cycle' || isCycleEnabled)
  const visibleAdminNavItems = isAdmin ? adminNavItems : []
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

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      setIsMobileNavOpen(false)
      setIsNotificationsOpen(false)
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [location.pathname])

  useEffect(() => {
    let isCancelled = false

    void (async () => {
      try {
        const [workoutData, goalData] = await Promise.all([fetchWorkouts(), fetchGoals().catch(() => null)])

        if (!isCancelled) {
          setNotifications(generateNotifications(workoutData, goalData))
        }
      } catch {
        if (!isCancelled) {
          setNotifications([])
        }
      }
    })()

    return () => {
      isCancelled = true
    }
  }, [location.pathname])

  useEffect(() => {
    let isCancelled = false

    void (async () => {
      try {
        const settings = await fetchCycleSettings()

        if (!isCancelled) {
          setIsCycleEnabled(settings.isEnabled)
        }
      } catch {
        if (!isCancelled) {
          setIsCycleEnabled(false)
        }
      }
    })()

    return () => {
      isCancelled = true
    }
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

  const visibleReadNotificationIds = useMemo(() => {
    const notificationIds = new Set(notifications.map((notification) => notification.id))
    return readNotificationIds.filter((notificationId) => notificationIds.has(notificationId))
  }, [notifications, readNotificationIds])
  const unreadCount = useMemo(
    () => notifications.filter((notification) => !visibleReadNotificationIds.includes(notification.id)).length,
    [notifications, visibleReadNotificationIds],
  )

  useEffect(() => {
    if (visibleReadNotificationIds.length !== readNotificationIds.length) {
      window.localStorage.setItem(
        NOTIFICATION_READ_STORAGE_KEY,
        JSON.stringify(visibleReadNotificationIds),
      )
    }
  }, [readNotificationIds.length, visibleReadNotificationIds])

  const sidebarPrimaryNavItems: StrideSidebarNavItem[] = visiblePrimaryNavItems.map((item) => ({
    to: item.to,
    label: item.label,
    icon: <AppIcon name={item.icon} />,
    meta: getNavMeta(item.to),
  }))
  const sidebarAdminNavItems: StrideSidebarNavItem[] = visibleAdminNavItems.map((item) => ({
    to: item.to,
    label: item.label,
    icon: <AppIcon name={item.icon} />,
    meta: 'Protected controls',
  }))

  return (
    <StrideShell
      isMobileNavOpen={isMobileNavOpen}
      onCloseMobileNav={() => setIsMobileNavOpen(false)}
      sidebar={(
        <StrideSidebar
          isOpen={isMobileNavOpen}
          brandName={APP_BRAND_NAME}
          brandKicker="FORGE"
          brandSubtitle="Track training, recovery, and progress in one controlled workspace."
          primaryNavItems={sidebarPrimaryNavItems}
          adminNavItems={sidebarAdminNavItems}
          onNavigate={() => setIsMobileNavOpen(false)}
          roleLabel={accountRole}
          accountLabel={accountLabel}
          accountEmail={authState?.user.email}
          accountInitials={accountInitials}
          themeToggleLabel={themeToggleLabel}
          themeButtonLabel={themeButtonLabel}
          themeIcon={<AppIcon name={theme === 'light' ? 'moon' : 'sun'} />}
          logoutIcon={<AppIcon name="logout" />}
          onToggleTheme={onToggleTheme}
          onLogout={logout}
        />
      )}
      topbar={(
        <StrideTopbar
          isMobileNavOpen={isMobileNavOpen}
          onToggleMobileNav={() => setIsMobileNavOpen((current) => !current)}
          navigationControlsId="primary-navigation"
          brandShort={APP_BRAND_SHORT}
          topbarMeta={topbarMeta}
          notificationCenterRef={notificationCenterRef}
          notifications={notifications}
          unreadCount={unreadCount}
          isNotificationsOpen={isNotificationsOpen}
          onToggleNotifications={() => setIsNotificationsOpen((current) => !current)}
          onMarkNotificationRead={markNotificationAsRead}
          onMarkAllNotificationsRead={markAllNotificationsAsRead}
          readNotificationIds={visibleReadNotificationIds}
          menuIcon={<HamburgerIcon />}
          notificationBellIcon={<BellIcon />}
          themeIcon={<ThemeIcon theme={theme} />}
          themeToggleLabel={themeToggleLabel}
          onToggleTheme={onToggleTheme}
          accountLabel={accountLabel}
          accountEmail={authState?.user.email}
          accountInitials={accountInitials}
          roleLabel={accountRole}
        />
      )}
    >
      <Outlet />
    </StrideShell>
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
    aiWorkout: Sparkles,
    nutrition: UtensilsCrossed,
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
