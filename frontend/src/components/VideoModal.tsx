import { useEffect, useId } from 'react'

type VideoModalProps = {
  title: string
  videoUrl: string
  onClose: () => void
}

type VideoPresentation =
  | {
      kind: 'direct'
      src: string
      mimeType?: string
    }
  | {
      kind: 'embed'
      src: string
      provider: string
      fallbackHref: string
    }
  | {
      kind: 'external'
      href: string
      reason: string
    }
  | {
      kind: 'unsupported'
      reason: string
    }

const DIRECT_VIDEO_TYPES: Record<string, string> = {
  '.m4v': 'video/mp4',
  '.mov': 'video/quicktime',
  '.mp4': 'video/mp4',
  '.ogv': 'video/ogg',
  '.ogg': 'video/ogg',
  '.webm': 'video/webm',
}

export function VideoModal({ title, videoUrl, onClose }: VideoModalProps) {
  const titleId = useId()
  const presentation = getVideoPresentation(videoUrl)

  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        onClose()
      }
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [onClose])

  return (
    <div className="modal-backdrop video-modal-backdrop" role="presentation" onClick={onClose}>
      <div
        className="modal-panel video-modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        onClick={(event) => event.stopPropagation()}
      >
        <div className="modal-header video-modal-header">
          <div className="video-modal-copy">
            <span className="stat-label">Exercise video</span>
            <h2 id={titleId}>{title}</h2>
            <p className="record-hint">Preview the demo without leaving the app.</p>
          </div>
          <button type="button" className="ghost-button compact-button" onClick={onClose}>
            Close
          </button>
        </div>

        <div className="video-modal-content">
          {presentation.kind === 'direct' ? (
            <div className="video-modal-player-shell">
              <video className="video-modal-player" controls preload="metadata" playsInline>
                <source src={presentation.src} type={presentation.mimeType} />
                Your browser could not play this video.
              </video>
            </div>
          ) : null}

          {presentation.kind === 'embed' ? (
            <div className="video-modal-player-shell">
              <iframe
                className="video-modal-frame"
                src={presentation.src}
                title={`${title} video demo`}
                loading="lazy"
                referrerPolicy="strict-origin-when-cross-origin"
                allow="accelerometer; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
                allowFullScreen
              />
            </div>
          ) : null}

          {presentation.kind === 'external' ? (
            <div className="video-modal-fallback">
              <strong>Embedded playback is unavailable</strong>
              <p>{presentation.reason}</p>
              <div className="video-modal-actions">
                <a
                  className="ghost-button compact-button video-modal-link"
                  href={presentation.href}
                  target="_blank"
                  rel="noreferrer"
                >
                  Open video in new tab
                </a>
              </div>
            </div>
          ) : null}

          {presentation.kind === 'unsupported' ? (
            <div className="video-modal-fallback">
              <strong>Video unavailable</strong>
              <p>{presentation.reason}</p>
            </div>
          ) : null}

          {presentation.kind === 'direct' ? (
            <div className="video-modal-actions">
              <span className="video-modal-note">If playback fails, open the source directly.</span>
              <a
                className="ghost-button compact-button video-modal-link"
                href={presentation.src}
                target="_blank"
                rel="noreferrer"
              >
                Open video in new tab
              </a>
            </div>
          ) : null}

          {presentation.kind === 'embed' ? (
            <div className="video-modal-actions">
              <span className="video-modal-note">Playback stays in-app unless you explicitly open the source.</span>
              <a
                className="ghost-button compact-button video-modal-link"
                href={presentation.fallbackHref}
                target="_blank"
                rel="noreferrer"
              >
                Open video in new tab
              </a>
            </div>
          ) : null}
        </div>
      </div>
    </div>
  )
}

function getVideoPresentation(rawUrl: string): VideoPresentation {
  let url: URL

  try {
    url = new URL(rawUrl)
  } catch {
    return {
      kind: 'unsupported',
      reason: 'This exercise video URL is invalid.',
    }
  }

  if (url.protocol !== 'https:' && url.protocol !== 'http:') {
    return {
      kind: 'unsupported',
      reason: 'This exercise video uses an unsupported URL protocol.',
    }
  }

  const directMimeType = getDirectVideoMimeType(url.pathname)
  if (directMimeType) {
    return {
      kind: 'direct',
      src: url.toString(),
      mimeType: directMimeType,
    }
  }

  const youTubeEmbedUrl = getYouTubeEmbedUrl(url)
  if (youTubeEmbedUrl) {
    return {
      kind: 'embed',
      src: youTubeEmbedUrl,
      provider: 'YouTube',
      fallbackHref: url.toString(),
    }
  }

  const vimeoEmbedUrl = getVimeoEmbedUrl(url)
  if (vimeoEmbedUrl) {
    return {
      kind: 'embed',
      src: vimeoEmbedUrl,
      provider: 'Vimeo',
      fallbackHref: url.toString(),
    }
  }

  return {
    kind: 'external',
    href: url.toString(),
    reason: 'This provider does not expose a known safe in-app embed URL here.',
  }
}

function getDirectVideoMimeType(pathname: string) {
  const normalizedPath = pathname.toLowerCase()

  for (const [extension, mimeType] of Object.entries(DIRECT_VIDEO_TYPES)) {
    if (normalizedPath.endsWith(extension)) {
      return mimeType
    }
  }

  return undefined
}

function getYouTubeEmbedUrl(url: URL) {
  const host = url.hostname.toLowerCase()

  if (host === 'youtu.be') {
    const videoId = url.pathname.split('/').filter(Boolean)[0]
    return videoId ? `https://www.youtube-nocookie.com/embed/${videoId}` : null
  }

  if (host === 'www.youtube.com' || host === 'youtube.com' || host === 'm.youtube.com' || host === 'youtube-nocookie.com' || host === 'www.youtube-nocookie.com') {
    const pathSegments = url.pathname.split('/').filter(Boolean)

    if (pathSegments[0] === 'watch') {
      const videoId = url.searchParams.get('v')
      return videoId ? `https://www.youtube-nocookie.com/embed/${videoId}` : null
    }

    if (pathSegments[0] === 'embed' && pathSegments[1]) {
      return `https://www.youtube-nocookie.com/embed/${pathSegments[1]}`
    }

    if ((pathSegments[0] === 'shorts' || pathSegments[0] === 'live') && pathSegments[1]) {
      return `https://www.youtube-nocookie.com/embed/${pathSegments[1]}`
    }
  }

  return null
}

function getVimeoEmbedUrl(url: URL) {
  const host = url.hostname.toLowerCase()
  const pathSegments = url.pathname.split('/').filter(Boolean)

  if ((host === 'vimeo.com' || host === 'www.vimeo.com') && pathSegments[0]) {
    return /^\d+$/.test(pathSegments[0]) ? `https://player.vimeo.com/video/${pathSegments[0]}` : null
  }

  if ((host === 'player.vimeo.com' || host === 'www.player.vimeo.com') && pathSegments[0] === 'video' && pathSegments[1]) {
    return /^\d+$/.test(pathSegments[1]) ? `https://player.vimeo.com/video/${pathSegments[1]}` : null
  }

  return null
}
