import { type ReactNode } from 'react'
import { NavLink, Navigate, Outlet, Route, Routes, useLocation } from 'react-router-dom'
import './App.css'
import { useAuth } from './auth/AuthContext'
import { DashboardPage } from './pages/DashboardPage'
import { ExerciseProgressPage } from './pages/ExerciseProgressPage'
import { LoginPage } from './pages/LoginPage'
import { ProfilePage } from './pages/ProfilePage'
import { RegisterPage } from './pages/RegisterPage'
import { WeightPage } from './pages/WeightPage'
import { WorkoutsPage } from './pages/WorkoutsPage'

function App() {
  return (
    <Routes>
      <Route path="/login" element={<PublicOnlyRoute><LoginPage /></PublicOnlyRoute>} />
      <Route path="/register" element={<PublicOnlyRoute><RegisterPage /></PublicOnlyRoute>} />
      <Route element={<ProtectedRoute />}>
        <Route path="/" element={<Navigate to="/dashboard" replace />} />
        <Route element={<AppLayout />}>
          <Route path="/dashboard" element={<DashboardPage />} />
          <Route path="/weight" element={<WeightPage />} />
          <Route path="/workouts" element={<WorkoutsPage />} />
          <Route path="/exercise-progress" element={<ExerciseProgressPage />} />
          <Route path="/profile" element={<ProfilePage />} />
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

function AppLayout() {
  const { authState, logout } = useAuth()
  const accountLabel = authState?.user.displayName || authState?.user.fullName || authState?.user.email

  return (
    <div className="app-shell">
      <header className="app-header">
        <div className="brand-block">
          <p className="brand-kicker">Training log</p>
          <p className="brand-name">Gym Tracker</p>
          <p className="brand-subtitle">Weight, workouts, progress, and planning in one place.</p>
        </div>

        <div className="header-actions">
          <nav className="main-nav" aria-label="Primary">
            <NavLink
              to="/dashboard"
              className={({ isActive }) => (isActive ? 'nav-link nav-link-active' : 'nav-link')}
            >
              Dashboard
            </NavLink>
            <NavLink
              to="/weight"
              className={({ isActive }) => (isActive ? 'nav-link nav-link-active' : 'nav-link')}
            >
              Weight
            </NavLink>
            <NavLink
              to="/workouts"
              className={({ isActive }) => (isActive ? 'nav-link nav-link-active' : 'nav-link')}
            >
              Workouts
            </NavLink>
            <NavLink
              to="/exercise-progress"
              className={({ isActive }) => (isActive ? 'nav-link nav-link-active' : 'nav-link')}
            >
              Exercise Progress
            </NavLink>
            <NavLink
              to="/profile"
              className={({ isActive }) => (isActive ? 'nav-link nav-link-active' : 'nav-link')}
            >
              Profile
            </NavLink>
          </nav>

          <div className="account-chip">
            <span title={authState?.user.email}>{accountLabel}</span>
            <button type="button" className="ghost-button" onClick={logout}>
              Log out
            </button>
          </div>
        </div>
      </header>

      <Outlet />
    </div>
  )
}

export default App
