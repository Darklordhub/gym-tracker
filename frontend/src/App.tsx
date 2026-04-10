import { useEffect, useState, type ReactNode } from 'react'
import { NavLink, Navigate, Outlet, Route, Routes, useLocation } from 'react-router-dom'
import './App.css'
import { useAuth } from './auth/AuthContext'
import { DashboardPage } from './pages/DashboardPage'
import { AdminPage } from './pages/AdminPage'
import { ExerciseProgressPage } from './pages/ExerciseProgressPage'
import { LoginPage } from './pages/LoginPage'
import { ProfilePage } from './pages/ProfilePage'
import { RegisterPage } from './pages/RegisterPage'
import { WeightPage } from './pages/WeightPage'
import { WorkoutsPage } from './pages/WorkoutsPage'

const primaryNavItems = [
  { to: '/dashboard', label: 'Dashboard' },
  { to: '/weight', label: 'Weight' },
  { to: '/workouts', label: 'Workouts' },
  { to: '/exercise-progress', label: 'Exercise Progress' },
  { to: '/profile', label: 'Profile' },
] as const

const THEME_STORAGE_KEY = 'gym-tracker-theme'

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
  const navItems = isAdmin ? [...primaryNavItems, { to: '/admin', label: 'Admin' as const }] : primaryNavItems
  const [isMobileNavOpen, setIsMobileNavOpen] = useState(false)

  useEffect(() => {
    setIsMobileNavOpen(false)
  }, [location.pathname])

  return (
    <div className="app-shell">
      <header className="app-header">
        <div className="app-header-top">
          <div className="brand-block">
            <p className="brand-kicker">Training log</p>
            <p className="brand-name">Gym Tracker</p>
            <p className="brand-subtitle">Weight, workouts, progress, and planning in one place.</p>
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

        <div className="header-actions">
          <button
            type="button"
            className="ghost-button mobile-nav-toggle"
            aria-expanded={isMobileNavOpen}
            aria-controls="primary-navigation"
            onClick={() => setIsMobileNavOpen((current) => !current)}
          >
            {isMobileNavOpen ? 'Close menu' : 'Open menu'}
          </button>

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

export default App
