import type { ReactNode } from 'react'
import { NavLink } from 'react-router-dom'

export type StrideSidebarNavItem = {
  to: string
  label: string
  icon: ReactNode
  meta: string
}

type StrideSidebarProps = {
  isOpen: boolean
  brandName: string
  brandKicker: string
  brandSubtitle: string
  primaryNavItems: readonly StrideSidebarNavItem[]
  adminNavItems: readonly StrideSidebarNavItem[]
  onNavigate: () => void
  roleLabel: string
  accountLabel?: string
  accountEmail?: string
  accountInitials: string
  themeToggleLabel: string
  themeButtonLabel: string
  themeIcon: ReactNode
  logoutIcon: ReactNode
  onToggleTheme: () => void
  onLogout: () => void
}

export function StrideSidebar({
  isOpen,
  brandName,
  brandKicker,
  brandSubtitle,
  primaryNavItems,
  adminNavItems,
  onNavigate,
  roleLabel,
  accountLabel,
  accountEmail,
  accountInitials,
  themeToggleLabel,
  themeButtonLabel,
  themeIcon,
  logoutIcon,
  onToggleTheme,
  onLogout,
}: StrideSidebarProps) {
  return (
    <aside
      id="primary-navigation"
      className={isOpen ? 'app-sidebar app-sidebar-open' : 'app-sidebar'}
    >
      <div className="sidebar-inner">
        <div className="sidebar-brand">
          <div className="brand-mark" aria-hidden="true">
            <span />
          </div>
          <div className="sidebar-brand-copy">
            <span className="brand-kicker">{brandKicker}</span>
            <strong className="brand-name">{brandName}</strong>
            <p className="brand-subtitle">{brandSubtitle}</p>
          </div>
        </div>

        <div className="sidebar-section">
          <div className="sidebar-section-header">
            <span>Main navigation</span>
            <span className="sidebar-section-count">{primaryNavItems.length}</span>
          </div>
          <nav className="sidebar-nav" aria-label="Main navigation">
            {primaryNavItems.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                title={item.label}
                className={({ isActive }) =>
                  isActive ? 'sidebar-link sidebar-link-active' : 'sidebar-link'
                }
                onClick={onNavigate}
              >
                <span className="sidebar-link-icon" aria-hidden="true">
                  {item.icon}
                </span>
                <span className="sidebar-link-copy">
                  <span className="sidebar-link-label">{item.label}</span>
                  <span className="sidebar-link-meta">{item.meta}</span>
                </span>
              </NavLink>
            ))}
          </nav>
        </div>

        {adminNavItems.length > 0 ? (
          <div className="sidebar-section sidebar-section-admin">
            <div className="sidebar-section-header">
              <span>Admin</span>
              <span className="sidebar-section-count">Restricted</span>
            </div>
            <nav className="sidebar-nav sidebar-nav-secondary" aria-label="Admin navigation">
              {adminNavItems.map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  title={item.label}
                  className={({ isActive }) =>
                    isActive ? 'sidebar-link sidebar-link-active' : 'sidebar-link'
                  }
                  onClick={onNavigate}
                >
                  <span className="sidebar-link-icon" aria-hidden="true">
                    {item.icon}
                  </span>
                  <span className="sidebar-link-copy">
                    <span className="sidebar-link-label">{item.label}</span>
                    <span className="sidebar-link-meta">{item.meta}</span>
                  </span>
                </NavLink>
              ))}
            </nav>
          </div>
        ) : null}

        <div className="sidebar-section sidebar-section-secondary">
          <div className="sidebar-section-header">
            <span>Account</span>
            <span className="status-pill status-pill-lime">{roleLabel}</span>
          </div>
          <div className="account-panel">
            <div className="user-pill">
              <div className="avatar-ring" aria-hidden="true">
                <div className="avatar-inner">{accountInitials}</div>
              </div>
              <div className="user-copy">
                <strong title={accountEmail}>{accountLabel}</strong>
                <span>{roleLabel} access</span>
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
                {themeIcon}
                <span className="sidebar-action-label">{themeButtonLabel}</span>
              </button>
              <button
                type="button"
                className="ghost-button sidebar-action-button"
                onClick={onLogout}
                aria-label="Log out"
                title="Log out"
              >
                {logoutIcon}
                <span className="sidebar-action-label">Log out</span>
              </button>
            </div>
          </div>
        </div>
      </div>
    </aside>
  )
}
