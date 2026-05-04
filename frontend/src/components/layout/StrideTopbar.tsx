import type { ReactNode, RefObject } from 'react'
import { formatDate } from '../../lib/format'
import type { AppNotification } from '../../lib/notifications'

export type StrideTopbarMeta = {
  title: string
  eyebrow: string
  description: string
}

type StrideTopbarProps = {
  isMobileNavOpen: boolean
  onToggleMobileNav: () => void
  navigationControlsId: string
  brandShort: string
  topbarMeta: StrideTopbarMeta
  notificationCenterRef: RefObject<HTMLDivElement | null>
  notifications: AppNotification[]
  unreadCount: number
  isNotificationsOpen: boolean
  onToggleNotifications: () => void
  onMarkNotificationRead: (notificationId: string) => void
  onMarkAllNotificationsRead: () => void
  readNotificationIds: string[]
  menuIcon: ReactNode
  notificationBellIcon: ReactNode
  themeIcon: ReactNode
  themeToggleLabel: string
  onToggleTheme: () => void
  accountLabel?: string
  accountEmail?: string
  accountInitials: string
  roleLabel: string
}

export function StrideTopbar({
  isMobileNavOpen,
  onToggleMobileNav,
  navigationControlsId,
  brandShort,
  topbarMeta,
  notificationCenterRef,
  notifications,
  unreadCount,
  isNotificationsOpen,
  onToggleNotifications,
  onMarkNotificationRead,
  onMarkAllNotificationsRead,
  readNotificationIds,
  menuIcon,
  notificationBellIcon,
  themeIcon,
  themeToggleLabel,
  onToggleTheme,
  accountLabel,
  accountEmail,
  accountInitials,
  roleLabel,
}: StrideTopbarProps) {
  return (
    <header className="app-topbar">
      <div className="topbar-left">
        <button
          type="button"
          className="topbar-icon-button mobile-nav-toggle"
          aria-label={isMobileNavOpen ? 'Close navigation menu' : 'Open navigation menu'}
          aria-expanded={isMobileNavOpen}
          aria-controls={navigationControlsId}
          onClick={onToggleMobileNav}
        >
          {menuIcon}
          <span className="sr-only">
            {isMobileNavOpen ? 'Close navigation menu' : 'Open navigation menu'}
          </span>
        </button>

        <div className="topbar-breadcrumb">
          <span>{brandShort}</span>
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
        <div className="topbar-actions-cluster">
          <NotificationCenter
            containerRef={notificationCenterRef}
            notifications={notifications}
            unreadCount={unreadCount}
            isOpen={isNotificationsOpen}
            onToggle={onToggleNotifications}
            onMarkRead={onMarkNotificationRead}
            onMarkAllRead={onMarkAllNotificationsRead}
            readNotificationIds={readNotificationIds}
            notificationBellIcon={notificationBellIcon}
          />

          <button
            type="button"
            className="topbar-icon-button"
            onClick={onToggleTheme}
            aria-label={themeToggleLabel}
            title={themeToggleLabel}
          >
            {themeIcon}
          </button>
        </div>

        <div className="topbar-user">
          <div className="avatar-ring" aria-hidden="true">
            <div className="avatar-inner">{accountInitials}</div>
          </div>
          <div className="topbar-user-copy">
            <strong title={accountEmail}>{accountLabel}</strong>
            <span>{roleLabel} account</span>
          </div>
        </div>
      </div>
    </header>
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
  notificationBellIcon,
}: {
  containerRef: RefObject<HTMLDivElement | null>
  notifications: AppNotification[]
  unreadCount: number
  isOpen: boolean
  onToggle: () => void
  onMarkRead: (notificationId: string) => void
  onMarkAllRead: () => void
  readNotificationIds: string[]
  notificationBellIcon: ReactNode
}) {
  return (
    <div className="notification-center" ref={containerRef}>
      <button
        type="button"
        className={
          isOpen
            ? 'topbar-icon-button notification-toggle notification-toggle-open'
            : 'topbar-icon-button notification-toggle'
        }
        aria-expanded={isOpen}
        aria-controls="notification-panel"
        aria-label={`Notifications${unreadCount > 0 ? `, ${unreadCount} unread` : ''}`}
        onClick={onToggle}
      >
        <span className="notification-bell" aria-hidden="true">
          {notificationBellIcon}
        </span>
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
