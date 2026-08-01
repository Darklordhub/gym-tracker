import type { ReactNode } from 'react'

type StrideShellProps = {
  isMobileNavOpen: boolean
  onCloseMobileNav: () => void
  sidebar: ReactNode
  topbar: ReactNode
  children: ReactNode
}

export function StrideShell({
  isMobileNavOpen,
  onCloseMobileNav,
  sidebar,
  topbar,
  children,
}: StrideShellProps) {
  return (
    <div className={isMobileNavOpen ? 'forge-shell forge-shell-nav-open' : 'forge-shell'}>
      <div
        className={isMobileNavOpen ? 'shell-backdrop shell-backdrop-visible' : 'shell-backdrop'}
        onClick={onCloseMobileNav}
        aria-hidden="true"
      />

      {sidebar}

      <div className="app-main">
        {topbar}

        <main className="app-content">
          <div className="content-container">{children}</div>
        </main>
      </div>
    </div>
  )
}
