import { NavLink, Navigate, Route, Routes } from 'react-router-dom'
import './App.css'
import { DashboardPage } from './pages/DashboardPage'
import { ExerciseProgressPage } from './pages/ExerciseProgressPage'
import { WeightPage } from './pages/WeightPage'
import { WorkoutsPage } from './pages/WorkoutsPage'

function App() {
  return (
    <div className="app-shell">
      <header className="app-header">
        <div className="brand-block">
          <p className="brand-kicker">Training log</p>
          <p className="brand-name">Gym Tracker</p>
          <p className="brand-subtitle">Weight, workouts, progress, and planning in one place.</p>
        </div>

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
        </nav>
      </header>

      <Routes>
        <Route path="/" element={<Navigate to="/dashboard" replace />} />
        <Route path="/dashboard" element={<DashboardPage />} />
        <Route path="/weight" element={<WeightPage />} />
        <Route path="/workouts" element={<WorkoutsPage />} />
        <Route path="/exercise-progress" element={<ExerciseProgressPage />} />
      </Routes>
    </div>
  )
}

export default App
