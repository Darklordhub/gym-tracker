import { useDeferredValue, useEffect, useMemo, useState } from 'react'
import {
  fetchExerciseCatalog,
  fetchExerciseCatalogItem,
  searchExerciseCatalog,
} from '../api/exerciseCatalog'
import { StateCard } from '../components/StateCard'
import { formatDate } from '../lib/format'
import { getRequestErrorMessage } from '../lib/http'
import type { ExerciseCatalogItem } from '../types/exerciseCatalog'

export function ExerciseLibraryPage() {
  const [items, setItems] = useState<ExerciseCatalogItem[]>([])
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [selectedItem, setSelectedItem] = useState<ExerciseCatalogItem | null>(null)
  const [searchQuery, setSearchQuery] = useState('')
  const [isLoadingList, setIsLoadingList] = useState(true)
  const [isLoadingDetails, setIsLoadingDetails] = useState(false)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [detailsErrorMessage, setDetailsErrorMessage] = useState<string | null>(null)
  const deferredSearchQuery = useDeferredValue(searchQuery)

  useEffect(() => {
    void loadCatalog(deferredSearchQuery)
  }, [deferredSearchQuery])

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
    if (selectedId === null) {
      return
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setSelectedId(null)
      }
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [selectedId])

  const activeMuscles = useMemo(() => {
    if (!selectedItem) {
      return []
    }

    return [selectedItem.primaryMuscle, ...selectedItem.secondaryMuscles].filter(
      (muscle): muscle is string => Boolean(muscle),
    )
  }, [selectedItem])

  async function loadCatalog(query: string) {
    try {
      setIsLoadingList(true)
      setErrorMessage(null)
      const nextItems = query.trim()
        ? await searchExerciseCatalog(query.trim())
        : await fetchExerciseCatalog()
      setItems(nextItems)
    } catch (error) {
      setErrorMessage(getRequestErrorMessage(error, 'Unable to load the exercise library.'))
      setItems([])
    } finally {
      setIsLoadingList(false)
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
            <strong>{items.length}</strong>
            <p>{items.length === 1 ? 'active exercise in view' : 'active exercises in view'}</p>
            <div className="forge-focus-pills">
              <span className="info-pill">{deferredSearchQuery.trim() ? 'Filtered' : 'Full library'}</span>
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
                deferredSearchQuery.trim()
                  ? 'Try a broader search term or clear the current filter.'
                  : 'No exercise catalog items are available yet.'
              }
            />
          ) : (
            <div className="exercise-library-list list-scroll-region">
              {items.map((item) => {
                const previewText = item.description ?? item.instructions ?? 'Open details for muscles, equipment, and instructions.'

                return (
                  <button
                    key={item.id}
                    type="button"
                    className={item.id === selectedId ? 'exercise-library-item exercise-library-item-active' : 'exercise-library-item'}
                    onClick={() => setSelectedId(item.id)}
                  >
                    {item.thumbnailUrl ? (
                      <div className="exercise-library-item-media">
                        <img src={item.thumbnailUrl} alt={item.name} />
                      </div>
                    ) : (
                      <div className="exercise-library-item-media exercise-library-item-media-fallback" aria-hidden="true">
                        <span>{item.name.slice(0, 1).toUpperCase()}</span>
                      </div>
                    )}
                    <div className="exercise-library-item-copy">
                      <strong>{item.name}</strong>
                      <p>{previewText}</p>
                    </div>
                    <div className="exercise-library-item-meta">
                      {item.primaryMuscle ? <span className="info-pill">{formatLabel(item.primaryMuscle)}</span> : null}
                      {item.equipment ? <span className="info-pill">{formatLabel(item.equipment)}</span> : null}
                      {item.difficulty ? <span className="info-pill">{formatLabel(item.difficulty)}</span> : null}
                    </div>
                  </button>
                )
              })}
            </div>
          )}
        </section>
      </section>

      {selectedId !== null ? (
        <ExerciseLibraryDetailsModal
          item={selectedItem}
          isLoading={isLoadingDetails}
          errorMessage={detailsErrorMessage}
          activeMuscles={activeMuscles}
          onClose={() => setSelectedId(null)}
        />
      ) : null}
    </main>
  )
}

function ExerciseLibraryDetailsModal({
  item,
  isLoading,
  errorMessage,
  activeMuscles,
  onClose,
}: {
  item: ExerciseCatalogItem | null
  isLoading: boolean
  errorMessage: string | null
  activeMuscles: string[]
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
            {item.thumbnailUrl ? (
              <div className="exercise-library-detail-media exercise-library-modal-media">
                <img src={item.thumbnailUrl} alt={item.name} />
              </div>
            ) : null}

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
                  <a className="ghost-button compact-button" href={item.videoUrl} target="_blank" rel="noreferrer">
                    Watch demo
                  </a>
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
