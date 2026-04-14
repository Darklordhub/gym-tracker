import type { Workout } from '../types/workout'
import type { CycleGuidance } from '../types/cycle'
import type { GoalSettings } from '../types/goals'
import type { ReadinessLog } from '../types/readiness'
import { countWorkoutsInWeek } from './workoutMetrics'

export type ExerciseSuggestion = {
  suggestedWeightKg: number
  reason: string
  confidenceLabel?: string
  prOpportunity?: {
    message: string
    targetWeightKg: number | null
  }
}

export type ExerciseSetHistoryItem = {
  date: string
  workoutId?: number
  setOrder: number
  reps: number
  weightKg: number
}

export type WorkoutAssistantInsight = {
  weeklyNudge: string
  todaySuggestion: {
    trainingType: 'strength' | 'cardio' | 'rest'
    title: string
    message: string
  }
  prOpportunity: {
    exerciseName: string
    message: string
    targetWeightKg: number | null
  } | null
  revisitSuggestions: Array<{
    exerciseName: string
    lastTrainedAt: string
    message: string
  }>
}

export function getExerciseHistory(workouts: Workout[], exerciseName: string) {
  const normalizedExerciseName = normalizeExerciseName(exerciseName)

  return workouts
    .flatMap<ExerciseSetHistoryItem>((workout) =>
      workout.exerciseEntries
        .filter((exercise) => normalizeExerciseName(exercise.exerciseName) === normalizedExerciseName)
        .flatMap((exercise) =>
          exercise.sets.map((set) => ({
            date: workout.date,
            workoutId: workout.id,
            setOrder: set.order,
            reps: set.reps,
            weightKg: set.weightKg,
          })),
        ),
    )
    .sort((left, right) => new Date(right.date).getTime() - new Date(left.date).getTime())
}

export function getSuggestedNextWeight(
  workouts: Workout[],
  exerciseName: string,
  setOrder: number | null,
  reps: number | null,
) {
  const history = getExerciseHistory(workouts, exerciseName)

  if (history.length === 0) {
    return null
  }

  const latest = history[0]
  const recentWindow = history.slice(0, 3)
  const recentAverage =
    recentWindow.length > 0
      ? Number(
          (
            recentWindow.reduce((sum, entry) => sum + entry.weightKg, 0) / recentWindow.length
          ).toFixed(1),
        )
      : latest.weightKg
  const personalBest = Math.max(...history.map((entry) => entry.weightKg))
  const prOpportunity = getPrOpportunity(latest.weightKg, personalBest)

  if (setOrder && reps) {
    const exactMatch = history.find((entry) => entry.setOrder === setOrder && entry.reps === reps)

    if (exactMatch) {
      return {
        suggestedWeightKg: getSuggestedIncrease(exactMatch.weightKg),
        reason: `Last set ${setOrder} for ${reps} reps was ${exactMatch.weightKg} kg.`,
        confidenceLabel: 'Matched exact set and reps',
        prOpportunity: prOpportunity ?? undefined,
      } satisfies ExerciseSuggestion
    }
  }

  if (reps) {
    const repMatch = history.find((entry) => entry.reps === reps)

    if (repMatch) {
      return {
        suggestedWeightKg: getSuggestedIncrease(repMatch.weightKg),
        reason: `Last ${reps}-rep set was ${repMatch.weightKg} kg.`,
        confidenceLabel: 'Matched recent reps',
        prOpportunity: prOpportunity ?? undefined,
      } satisfies ExerciseSuggestion
    }
  }

  return {
    suggestedWeightKg: roundToNearestIncrement(recentAverage),
    reason: `Based on your last ${recentWindow.length} logged set${recentWindow.length === 1 ? '' : 's'}, ${recentAverage} kg is the current working baseline.`,
    confidenceLabel: 'Recent-set average',
    prOpportunity: prOpportunity ?? undefined,
  } satisfies ExerciseSuggestion
}

export function getWorkoutAssistantInsight(
  workouts: Workout[],
  goals: GoalSettings | null,
  cycleGuidance?: CycleGuidance | null,
  readinessLog?: ReadinessLog | null,
  now = new Date(),
): WorkoutAssistantInsight {
  const workoutsThisWeek = countWorkoutsInWeek(workouts, now)

  const weeklyTarget = goals?.weeklyWorkoutTarget ?? null
  const weeklyNudge =
    weeklyTarget === null
      ? 'Set a weekly workout target to unlock consistency reminders.'
      : workoutsThisWeek >= weeklyTarget
        ? `You have already hit your weekly goal with ${workoutsThisWeek} of ${weeklyTarget} planned workouts logged.`
        : `${weeklyTarget - workoutsThisWeek} more workout${weeklyTarget - workoutsThisWeek === 1 ? '' : 's'} would keep you on track this week.`

  const exerciseStats = buildExerciseStats(workouts)

  const prOpportunity =
    exerciseStats.size === 0
      ? null
      : [...exerciseStats.values()]
          .filter((entry) => entry.recentBestWeightKg >= entry.personalBestWeightKg - 2.5)
          .sort((left, right) => right.recentBestWeightKg - left.recentBestWeightKg)[0] ?? null

  const revisitSuggestions = [...exerciseStats.values()]
    .filter((entry) => entry.daysSinceLastSession >= 7)
    .sort((left, right) => right.daysSinceLastSession - left.daysSinceLastSession)
    .slice(0, 3)
    .map((entry) => ({
      exerciseName: entry.exerciseName,
      lastTrainedAt: entry.lastSessionDate,
      message: `${entry.exerciseName} has not been trained for ${entry.daysSinceLastSession} day${entry.daysSinceLastSession === 1 ? '' : 's'}.`,
    }))

  return {
    weeklyNudge,
    todaySuggestion: getDailyTrainingSuggestion(workouts, cycleGuidance, readinessLog, now),
    prOpportunity: prOpportunity
      ? {
          exerciseName: prOpportunity.exerciseName,
          message:
            prOpportunity.recentBestWeightKg >= prOpportunity.personalBestWeightKg
              ? `${prOpportunity.exerciseName} is already matching your best recent work. If the session feels good, a small PR attempt may be there.`
              : `${prOpportunity.exerciseName} is within ${Number((prOpportunity.personalBestWeightKg - prOpportunity.recentBestWeightKg).toFixed(1))} kg of your best lift.`,
          targetWeightKg: getSuggestedIncrease(prOpportunity.personalBestWeightKg),
        }
      : null,
    revisitSuggestions,
  }
}

export function getDailyTrainingSuggestion(
  workouts: Workout[],
  cycleGuidance?: CycleGuidance | null,
  readinessLog?: ReadinessLog | null,
  now = new Date(),
) {
  const recentWindowStart = new Date(now)
  recentWindowStart.setHours(0, 0, 0, 0)
  recentWindowStart.setDate(recentWindowStart.getDate() - 6)

  const recentWorkouts = workouts.filter((workout) => new Date(workout.date) >= recentWindowStart)
  const recentStrengthSessions = recentWorkouts.filter((workout) => workout.workoutType !== 'cardio')
  const recentCardioSessions = recentWorkouts.filter((workout) => workout.workoutType === 'cardio')
  const recentSetCount = recentStrengthSessions.reduce(
    (count, workout) => count + workout.exerciseEntries.reduce((sum, exercise) => sum + exercise.sets.length, 0),
    0,
  )
  const recentCardioMinutes = recentCardioSessions.reduce(
    (total, workout) => total + (workout.cardioDurationMinutes ?? 0),
    0,
  )

  const highTrainingLoad =
    recentStrengthSessions.length >= 4 || recentSetCount >= 36 || recentCardioMinutes >= 180
  const moderateTrainingLoad =
    recentStrengthSessions.length >= 3 || recentSetCount >= 22 || recentCardioMinutes >= 90

  const highFatigueSignals =
    Boolean(cycleGuidance?.isHigherFatiguePhase) ||
    cycleGuidance?.symptomLoadLabel === 'High' ||
    (cycleGuidance?.recentRecoveryFeeling !== null &&
      cycleGuidance?.recentRecoveryFeeling !== undefined &&
      cycleGuidance.recentRecoveryFeeling <= 2) ||
    (cycleGuidance?.recentSleepQuality !== null &&
      cycleGuidance?.recentSleepQuality !== undefined &&
      cycleGuidance.recentSleepQuality <= 2)

  const lowFatigueSignals =
    Boolean(cycleGuidance?.isEnabled) &&
    cycleGuidance?.isHigherFatiguePhase === false &&
    cycleGuidance?.symptomLoadLabel !== 'High' &&
    (cycleGuidance?.recentRecoveryFeeling === null ||
      cycleGuidance?.recentRecoveryFeeling === undefined ||
      cycleGuidance.recentRecoveryFeeling >= 3)

  const readiness = getCurrentReadinessSignal(readinessLog, now)

  if (readiness?.level === 'low') {
    if (highTrainingLoad || highFatigueSignals) {
      return {
        trainingType: 'rest' as const,
        title: 'Recovery should lead today',
        message:
          'Today’s check-in points to low readiness, and your broader recovery signals are not asking for more stress. Rest or keep movement very light.',
      }
    }

    return {
      trainingType: 'cardio' as const,
      title: 'Keep it easy today',
      message:
        'Today’s check-in points to lower readiness. Easy cardio or a short walk is a better fit than a demanding strength session.',
    }
  }

  if (highFatigueSignals && highTrainingLoad) {
    return {
      trainingType: 'rest' as const,
      title: 'Rest or keep movement very light',
      message:
        readiness?.level === 'high'
          ? 'You feel ready, but recent load and fatigue signals are still pointing toward recovery. Keep today light rather than forcing another hard session.'
          : 'Recent load is already high and your recovery signals look strained. A full rest day or a short easy walk is the better call today.',
    }
  }

  if (highFatigueSignals) {
    return {
      trainingType: 'cardio' as const,
      title: 'Low-intensity cardio fits better today',
      message:
        readiness?.level === 'high'
          ? 'Your check-in looks solid, but broader fatigue signals still favor a walk, easy cycle, or other low-intensity cardio over a hard strength session.'
          : 'Recovery signals look softer today. A walk, easy cycle, or other low-intensity cardio session is likely a better fit than another hard strength workout.',
    }
  }

  if (readiness?.level === 'medium' && moderateTrainingLoad) {
    return {
      trainingType: 'cardio' as const,
      title: 'Choose a moderate day',
      message:
        'Today’s check-in is steady rather than sharp, and recent load is already building. Recovery cardio or a controlled strength session would both make sense.',
    }
  }

  if (moderateTrainingLoad) {
    return {
      trainingType: 'cardio' as const,
      title: 'Recovery cardio is a solid option',
      message:
        'Recent strength load is building. Low-intensity cardio can keep you moving without stacking another demanding lifting session.',
    }
  }

  if (lowFatigueSignals) {
    return {
      trainingType: 'strength' as const,
      title: 'Strength work looks well supported',
      message:
        cycleGuidance?.estimatedCurrentPhase === 'Follicular' || cycleGuidance?.estimatedCurrentPhase === 'Ovulatory'
          ? 'Recovery looks solid and this phase can often tolerate harder work well. Strength or higher-intensity work is reasonable if the session feels sharp.'
          : 'Recovery looks good and recent load is manageable. A focused strength session should fit well today.',
    }
  }

  if (readiness?.level === 'high') {
    return {
      trainingType: 'strength' as const,
      title: 'You look ready for strength work',
      message:
        'Today’s check-in looks strong and recent load is manageable. A focused strength session or a higher-quality main lift should fit well.',
    }
  }

  if (readiness?.level === 'medium') {
    return {
      trainingType: 'cardio' as const,
      title: 'A balanced session fits best',
      message:
        'Today’s check-in looks steady. Moderate cardio or a controlled strength session both fit, but there is no strong case for pushing hard.',
    }
  }

  if (recentWorkouts.length === 0) {
    return {
      trainingType: 'strength' as const,
      title: 'Start with a manageable session',
      message:
        'You do not have much recent training load yet. A straightforward strength session is a good default if energy feels normal.',
    }
  }

  return {
    trainingType: 'cardio' as const,
    title: 'Cardio is the balanced option today',
    message:
      'Recent load is manageable, and cardio is a balanced way to stay active without asking for as much recovery as another full strength day.',
  }
}

function getCurrentReadinessSignal(readinessLog: ReadinessLog | null | undefined, now: Date) {
  if (!readinessLog) {
    return null
  }

  const today = now.toISOString().slice(0, 10)
  if (readinessLog.date !== today) {
    return null
  }

  if (readinessLog.readinessScore >= 2.5) {
    return { level: 'high' as const }
  }

  if (readinessLog.readinessScore >= 1.9) {
    return { level: 'medium' as const }
  }

  return { level: 'low' as const }
}

function getSuggestedIncrease(weightKg: number) {
  return roundToNearestIncrement(weightKg >= 20 ? weightKg + 2.5 : weightKg + 1)
}

function roundToNearestIncrement(value: number) {
  return Math.round(value * 2) / 2
}

function normalizeExerciseName(exerciseName: string) {
  return exerciseName.trim().toUpperCase()
}

function getPrOpportunity(currentWeightKg: number, personalBestWeightKg: number) {
  if (currentWeightKg >= personalBestWeightKg) {
    return {
      message: 'You are already at your best logged weight. A small increase could set a new PR.',
      targetWeightKg: getSuggestedIncrease(personalBestWeightKg),
    }
  }

  if (currentWeightKg >= personalBestWeightKg - 2.5) {
    return {
      message: `You are within ${Number((personalBestWeightKg - currentWeightKg).toFixed(1))} kg of your best logged weight.`,
      targetWeightKg: personalBestWeightKg,
    }
  }

  return null
}

function buildExerciseStats(workouts: Workout[]) {
  const stats = new Map<
    string,
    {
      exerciseName: string
      personalBestWeightKg: number
      recentBestWeightKg: number
      lastSessionDate: string
      daysSinceLastSession: number
    }
  >()

  const now = new Date()

  for (const workout of workouts) {
    if (workout.workoutType === 'cardio') {
      continue
    }

    for (const exercise of workout.exerciseEntries) {
      const key = normalizeExerciseName(exercise.exerciseName)
      const workoutDate = workout.date
      const bestSetWeight = Math.max(...exercise.sets.map((set) => set.weightKg))
      const current = stats.get(key)

      if (!current) {
        stats.set(key, {
          exerciseName: exercise.exerciseName,
          personalBestWeightKg: exercise.personalRecordWeightKg,
          recentBestWeightKg: bestSetWeight,
          lastSessionDate: workoutDate,
          daysSinceLastSession: getDaysSince(workoutDate, now),
        })
        continue
      }

      const isNewer = new Date(workoutDate).getTime() > new Date(current.lastSessionDate).getTime()

      stats.set(key, {
        exerciseName: current.exerciseName,
        personalBestWeightKg: Math.max(current.personalBestWeightKg, exercise.personalRecordWeightKg),
        recentBestWeightKg: isNewer ? bestSetWeight : current.recentBestWeightKg,
        lastSessionDate: isNewer ? workoutDate : current.lastSessionDate,
        daysSinceLastSession: isNewer ? getDaysSince(workoutDate, now) : current.daysSinceLastSession,
      })
    }
  }

  return stats
}

function getDaysSince(date: string, now: Date) {
  const target = new Date(date)
  target.setHours(0, 0, 0, 0)
  const current = new Date(now)
  current.setHours(0, 0, 0, 0)
  return Math.max(0, Math.round((current.getTime() - target.getTime()) / 86400000))
}
