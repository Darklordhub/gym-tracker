import { useDeferredValue, useEffect, useMemo, useRef, useState } from 'react'
import { Dumbbell, PlayCircle } from 'lucide-react'
import {
  fetchExerciseCatalogPage,
  fetchExerciseCatalogItem,
  searchExerciseCatalogPage,
} from '../api/exerciseCatalog'
import { apiClient } from '../lib/http'
import { StateCard } from '../components/StateCard'
import { VideoModal } from '../components/VideoModal'
import { formatDate } from '../lib/format'
import { getRequestErrorMessage } from '../lib/http'
import type { ExerciseCatalogItem } from '../types/exerciseCatalog'

const CATALOG_PAGE_SIZE = 24

export function ExerciseLibraryPage() {
  const [items, setItems] = useState<ExerciseCatalogItem[]>([])
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [selectedItem, setSelectedItem] = useState<ExerciseCatalogItem | null>(null)
  const [videoTarget, setVideoTarget] = useState<{ title: string; url: string } | null>(null)
  const [searchQuery, setSearchQuery] = useState('')
  const [page, setPage] = useState(1)
  const [totalCount, setTotalCount] = useState(0)
  const [isLoadingList, setIsLoadingList] = useState(true)
  const [isLoadingMore, setIsLoadingMore] = useState(false)
  const [isLoadingDetails, setIsLoadingDetails] = useState(false)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [detailsErrorMessage, setDetailsErrorMessage] = useState<string | null>(null)
  const [brokenThumbnails, setBrokenThumbnails] = useState<Record<number, true>>({})
  const deferredSearchQuery = useDeferredValue(searchQuery)
  const latestCatalogRequestRef = useRef(0)
  const normalizedSearchQuery = deferredSearchQuery.trim()

  useEffect(() => {
    void loadCatalogPage(normalizedSearchQuery, 1, false)
  }, [normalizedSearchQuery])

  useEffect(() => {
    if (items.length === 0) {
      setSelectedId(null)
      setSelectedItem(null)
      return
    }

    if (selectedId !== null && !items.some((item) => item.id === selectedId)) {
      setSelectedId(null)
    }
  }, [items, selectedId])

  useEffect(() => {
    if (selectedId === null) {
      setSelectedItem(null)
      setDetailsErrorMessage(null)
      return
    }

    void loadCatalogItem(selectedId)
  }, [selectedId])

  useEffect(() => {
    if (selectedId === null || videoTarget) {
      return
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setSelectedId(null)
      }
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [selectedId, videoTarget])

  const activeMuscles = useMemo(() => {
    if (!selectedItem) {
      return []
    }

    return [selectedItem.primaryMuscle, ...selectedItem.secondaryMuscles].filter(
      (muscle): muscle is string => Boolean(muscle),
    )
  }, [selectedItem])

  const hasMoreItems = items.length < totalCount
  const isSearchActive = normalizedSearchQuery.length > 0

  async function loadCatalogPage(query: string, nextPage: number, append: boolean) {
    const requestId = ++latestCatalogRequestRef.current

    try {
      if (append) {
        setIsLoadingMore(true)
      } else {
        setIsLoadingList(true)
        setErrorMessage(null)
      }

      const response = query
        ? await searchExerciseCatalogPage(query, nextPage, CATALOG_PAGE_SIZE)
        : await fetchExerciseCatalogPage(nextPage, CATALOG_PAGE_SIZE)

      if (requestId !== latestCatalogRequestRef.current) {
        return
      }

      setPage(response.page)
      setTotalCount(response.totalCount)
      setItems((currentItems) => {
        if (!append) {
          return response.items
        }

        const existingIds = new Set(currentItems.map((item) => item.id))
        return [...currentItems, ...response.items.filter((item) => !existingIds.has(item.id))]
      })
      setBrokenThumbnails((current) => {
        if (append) {
          return current
        }

        const next: Record<number, true> = {}
        for (const item of response.items) {
          if (current[item.id]) {
            next[item.id] = true
          }
        }
        return next
      })
    } catch (error) {
      if (requestId !== latestCatalogRequestRef.current) {
        return
      }

      setErrorMessage(getRequestErrorMessage(error, 'Unable to load the exercise library.'))
      if (!append) {
        setItems([])
        setTotalCount(0)
      }
    } finally {
      if (requestId === latestCatalogRequestRef.current) {
        setIsLoadingList(false)
        setIsLoadingMore(false)
      }
    }
  }

  async function loadCatalogItem(id: number) {
    try {
      setIsLoadingDetails(true)
      setDetailsErrorMessage(null)
      const item = await fetchExerciseCatalogItem(id)
      setSelectedItem(item)
    } catch (error) {
      setDetailsErrorMessage(getRequestErrorMessage(error, 'Unable to load exercise details.'))
      setSelectedItem(null)
    } finally {
      setIsLoadingDetails(false)
    }
  }

  function markThumbnailBroken(itemId: number) {
    setBrokenThumbnails((current) =>
      current[itemId]
        ? current
        : {
            ...current,
            [itemId]: true,
          },
    )
  }

  return (
    <main className="page-shell exercise-library-shell">
      <section className="hero-panel exercise-library-hero">
        <div className="exercise-library-hero-copy">
          <span className="eyebrow">FORGE / Catalog</span>
          <h1>Exercise Library</h1>
          <p className="hero-text">
            Browse the local exercise catalog, review movement details, and prepare for future synced data without
            depending on a live provider yet.
          </p>
        </div>
        <div className="exercise-library-hero-stats">
          <article className="forge-focus-card">
            <span className="stat-label">Catalog status</span>
            <strong>{totalCount}</strong>
            <p>
              Showing {items.length} of {totalCount}
              {totalCount === 1 ? ' active exercise' : ' active exercises'}
            </p>
            <div className="forge-focus-pills">
              <span className="info-pill">{isSearchActive ? 'Filtered' : 'Full library'}</span>
              <span className="info-pill info-pill-strength">Local source</span>
            </div>
          </article>
        </div>
      </section>

      <section className="exercise-library-grid">
        <section className="panel exercise-library-panel exercise-library-panel-full">
          <div className="panel-header">
            <div>
              <h2>Directory</h2>
              <p>Search by exercise name, equipment, or target muscle group, then open the exercise details popup.</p>
            </div>
          </div>

          <div className="exercise-library-toolbar">
            <label className="field">
              <span>Search</span>
              <input
                type="search"
                value={searchQuery}
                onChange={(event) => setSearchQuery(event.target.value)}
                placeholder="Search exercises, muscles, or equipment"
              />
            </label>
          </div>

          {errorMessage ? (
            <StateCard title="Library unavailable" description={errorMessage} tone="error" />
          ) : isLoadingList ? (
            <StateCard title="Loading library" description="Pulling the current exercise catalog." loading />
          ) : items.length === 0 ? (
            <StateCard
              title="No exercises found"
              description={
                isSearchActive
                  ? 'Try a broader search term or clear the current filter.'
                  : 'No exercise catalog items are available yet.'
              }
            />
          ) : (
            <>
              <div className="exercise-library-list list-scroll-region">
                {items.map((item) => {
                  const previewText = item.description ?? item.instructions ?? 'Open details for muscles, equipment, and instructions.'

                  return (
                    <button
                      key={item.id}
                      type="button"
                      className={item.id === selectedId ? 'exercise-card exercise-card-active' : 'exercise-card'}
                      onClick={() => setSelectedId(item.id)}
                    >
                      <ExerciseLibraryMedia
                        item={item}
                        isBroken={Boolean(brokenThumbnails[item.id])}
                        onError={() => markThumbnailBroken(item.id)}
                      />
                      <div className="exercise-card-body">
                        <strong className="exercise-card-title">{item.name}</strong>
                        <p className="exercise-card-description">{previewText}</p>
                      </div>
                      <div className="exercise-card-chips">
                        {item.primaryMuscle ? <span className="info-pill">{formatLabel(item.primaryMuscle)}</span> : null}
                        {item.equipment ? <span className="info-pill">{formatLabel(item.equipment)}</span> : null}
                        {item.difficulty ? <span className="info-pill">{formatLabel(item.difficulty)}</span> : null}
                      </div>
                    </button>
                  )
                })}
              </div>

              {hasMoreItems ? (
                <div className="exercise-library-list-actions">
                  <button
                    type="button"
                    className="ghost-button"
                    onClick={() => void loadCatalogPage(normalizedSearchQuery, page + 1, true)}
                    disabled={isLoadingMore}
                  >
                    {isLoadingMore ? 'Loading more...' : 'Load more'}
                  </button>
                  <span className="record-hint">
                    Showing {items.length} of {totalCount}
                    {isSearchActive ? ' matching exercises' : ' exercises'}
                  </span>
                </div>
              ) : totalCount > 0 ? (
                <div className="exercise-library-list-actions">
                  <span className="record-hint">
                    Showing all {totalCount}
                    {isSearchActive ? ' matching exercises' : ' exercises'}
                  </span>
                </div>
              ) : null}
            </>
          )}
        </section>
      </section>

      {selectedId !== null ? (
        <ExerciseLibraryDetailsModal
          item={selectedItem}
          isLoading={isLoadingDetails}
          errorMessage={detailsErrorMessage}
          activeMuscles={activeMuscles}
          onOpenVideo={(title, url) => setVideoTarget({ title, url })}
          onClose={() => setSelectedId(null)}
        />
      ) : null}

      {videoTarget ? (
        <VideoModal title={videoTarget.title} videoUrl={videoTarget.url} onClose={() => setVideoTarget(null)} />
      ) : null}
    </main>
  )
}

function ExerciseLibraryDetailsModal({
  item,
  isLoading,
  errorMessage,
  activeMuscles,
  onOpenVideo,
  onClose,
}: {
  item: ExerciseCatalogItem | null
  isLoading: boolean
  errorMessage: string | null
  activeMuscles: string[]
  onOpenVideo: (title: string, url: string) => void
  onClose: () => void
}) {
  return (
    <div className="modal-backdrop" role="presentation" onClick={onClose}>
      <div
        className="modal-panel exercise-library-modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby="exercise-library-modal-title"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="modal-header exercise-library-modal-header">
          <div>
            <span className="stat-label">Exercise details</span>
            <h2 id="exercise-library-modal-title">{item?.name ?? 'Loading exercise'}</h2>
            <p className="record-hint">Local catalog data with provider sync support.</p>
          </div>
          <button type="button" className="ghost-button compact-button" onClick={onClose}>
            Close
          </button>
        </div>

        {errorMessage ? (
          <StateCard title="Details unavailable" description={errorMessage} tone="error" />
        ) : isLoading ? (
          <StateCard title="Loading exercise" description="Pulling the latest local catalog details." loading />
        ) : !item ? (
          <StateCard title="Exercise unavailable" description="This exercise could not be loaded." />
        ) : (
          <div className="exercise-library-modal-body">
            <ExerciseLibraryMedia
              item={item}
              isBroken={false}
              onError={() => undefined}
              loading="eager"
              className="exercise-library-detail-media exercise-library-modal-media"
            />

            <div className="exercise-library-detail-copy">
              <div className="exercise-library-detail-heading">
                <span className="eyebrow">Catalog Entry</span>
                <h3>{item.name}</h3>
                <p>{item.description ?? 'This catalog entry is ready for richer descriptions later.'}</p>
              </div>

              <div className="exercise-library-detail-pills">
                <span className="info-pill">{item.source}</span>
                {item.equipment ? <span className="info-pill">{formatLabel(item.equipment)}</span> : null}
                {item.difficulty ? <span className="info-pill">{formatLabel(item.difficulty)}</span> : null}
                {item.isActive ? <span className="info-pill info-pill-strength">Active</span> : null}
              </div>

              <div className="exercise-library-detail-grid">
                <div className="exercise-library-detail-section">
                  <span className="stat-label">Muscles</span>
                  <div className="exercise-library-detail-pills">
                    {activeMuscles.length > 0 ? (
                      activeMuscles.map((muscle) => (
                        <span key={muscle} className="info-pill">
                          {formatLabel(muscle)}
                        </span>
                      ))
                    ) : (
                      <span className="record-hint">No muscle tags yet.</span>
                    )}
                  </div>
                </div>

                <div className="exercise-library-detail-section">
                  <span className="stat-label">Instructions</span>
                  <p>{item.instructions ?? 'Instruction steps can be added as the catalog grows.'}</p>
                </div>
              </div>

              <div className="exercise-library-detail-footer">
                {item.videoUrl ? (
                  <button
                    type="button"
                    className="ghost-button compact-button"
                    onClick={() => onOpenVideo(item.name, item.videoUrl!)}
                  >
                    <PlayCircle aria-hidden="true" focusable="false" strokeWidth={1.9} />
                    Watch demo
                  </button>
                ) : null}
                <span className="record-hint">
                  Updated {formatDate(item.updatedAt)}
                  {item.lastSyncedAt ? ` · Synced ${formatDate(item.lastSyncedAt)}` : ''}
                </span>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}

function formatLabel(value: string) {
  return value
    .split(/[\s_-]+/)
    .filter(Boolean)
    .map((part) => part[0]?.toUpperCase() + part.slice(1))
    .join(' ')
}

function ExerciseLibraryMedia({
  item,
  isBroken,
  onError,
  loading = 'lazy',
  className = 'exercise-card-media',
}: {
  item: ExerciseCatalogItem
  isBroken: boolean
  onError: () => void
  loading?: 'eager' | 'lazy'
  className?: string
}) {
  const thumbnailUrl = resolveExerciseLibraryMediaUrl(item.thumbnailUrl ?? item.localMediaPath)
  const [hasInternalError, setHasInternalError] = useState(false)
  const [hasLoaded, setHasLoaded] = useState(false)

  useEffect(() => {
    setHasInternalError(false)
    setHasLoaded(false)
  }, [thumbnailUrl])

  const showImage = Boolean(thumbnailUrl) && !isBroken && !hasInternalError

  return (
    <div className={showImage ? className : `${className} exercise-library-item-media-fallback`} aria-hidden={showImage ? undefined : 'true'}>
      {showImage ? (
        <img
          src={thumbnailUrl!}
          alt={item.name}
          loading={loading}
          decoding="async"
          referrerPolicy="no-referrer"
          className={hasLoaded ? 'is-loaded' : undefined}
          onLoad={() => setHasLoaded(true)}
          onError={(event) => {
            event.currentTarget.style.display = 'none'
            setHasInternalError(true)
            onError()
          }}
        />
      ) : (
        <ExerciseLibraryMediaPlaceholder name={item.name} />
      )}
    </div>
  )
}

function ExerciseLibraryMediaPlaceholder({
  name,
  showAnimatedLabel = false,
}: {
  name: string
  showAnimatedLabel?: boolean
}) {
  return (
    <>
      <span className="exercise-library-item-media-fallback-icon">
        <Dumbbell aria-hidden="true" focusable="false" strokeWidth={1.8} />
      </span>
      <span className="exercise-library-item-media-fallback-initial">
        {name.slice(0, 1).toUpperCase()}
      </span>
      {showAnimatedLabel ? <span className="record-hint">Animated preview</span> : null}
    </>
  )
}

function resolveExerciseLibraryMediaUrl(value: string | null | undefined) {
  const normalizedValue = value?.trim()
  if (!normalizedValue) {
    return null
  }

  if (normalizedValue.startsWith('data:image/')) {
    return normalizedValue
  }

  const mediaOrigin = resolveExerciseLibraryMediaOrigin()

  try {
    const absoluteUrl = normalizedValue.startsWith('//')
      ? new URL(`${window.location.protocol}${normalizedValue}`)
      : new URL(normalizedValue, `${mediaOrigin}/`)

    if (!['http:', 'https:', 'data:', 'blob:'].includes(absoluteUrl.protocol)) {
      return null
    }

    return absoluteUrl.toString()
  } catch {
    return null
  }
}

function resolveExerciseLibraryMediaOrigin() {
  if (typeof window === 'undefined') {
    return 'http://localhost'
  }

  const configuredApiBaseUrl = typeof apiClient.defaults.baseURL === 'string'
    ? apiClient.defaults.baseURL
    : '/api'

  try {
    return new URL(configuredApiBaseUrl, window.location.origin).origin
  } catch {
    return window.location.origin
  }
}
