type StateCardProps = {
  title: string
  description: string
  tone?: 'default' | 'error'
  loading?: boolean
}

export function StateCard({
  title,
  description,
  tone = 'default',
  loading = false,
}: StateCardProps) {
  return (
    <div
      className={
        loading
          ? 'state-card loading-state'
          : tone === 'error'
            ? 'state-card error-state'
            : 'state-card'
      }
    >
      <div className="state-card-badge" aria-hidden="true">
        {loading ? '...' : tone === 'error' ? '!' : '0'}
      </div>
      <div className="state-card-copy">
        <strong>{title}</strong>
        <p>{description}</p>
      </div>
    </div>
  )
}
