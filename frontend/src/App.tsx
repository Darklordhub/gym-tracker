import { NavLink, Navigate, Route, Routes } from 'react-router-dom'
import './App.css'
import { WeightPage } from './pages/WeightPage'
import { WorkoutsPage } from './pages/WorkoutsPage'

function App() {
  return (
    <div className="app-shell">
      <header className="app-header">
        <div>
          <p className="brand-kicker">Training log</p>
          <p className="brand-name">Gym Tracker</p>
        </div>

        <nav className="main-nav" aria-label="Primary">
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
        </nav>
      </header>

      <Routes>
        <Route path="/" element={<Navigate to="/weight" replace />} />
        <Route path="/weight" element={<WeightPage />} />
        <Route path="/workouts" element={<WorkoutsPage />} />
      </Routes>
    </div>
  )
}

export default App
