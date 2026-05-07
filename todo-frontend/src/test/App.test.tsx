import { MockedProvider } from '@apollo/client/testing/react'
import type { MockedResponse } from '@apollo/client/testing'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import App, { CREATE_CALENDAR_EVENT, CREATE_TASK, GET_CALENDAR_EVENTS, GET_FAMILIES, GET_FAMILY_MEMBERS, GET_SCHEDULED_TASKS, GET_TASKS } from '../App'

const family = { id: 'family-1', name: 'Smith Family', boardId: 'family-home' }
const otherFamily = { id: 'family-2', name: 'Garcia Family', boardId: 'garcia-family' }
const emma = { id: 'member-1', familyId: 'family-1', name: 'Emma', color: '#6dbec2' }

const task = {
  id: 'task-1',
  familyId: 'family-1',
  title: 'Clean room',
  assigneeName: 'Emma',
  status: 'TODO' as const,
  completed: false,
  sortOrder: 0,
  dueAt: null,
  durationMinutes: null,
  recurrenceRule: 'None',
  updatedAt: '2026-04-09T15:30:00.000Z',
}

const calendarEvent = {
  id: 'event-1',
  familyId: 'family-1',
  memberId: 'member-1',
  title: 'Grocery Run',
  startAt: buildDateTime(new Date(), '10:00').toISOString(),
  endAt: buildDateTime(new Date(), '11:00').toISOString(),
  notes: 'Bring coupons',
  tone: '#6dbec2',
}

function currentRange(mode: 'week' | 'month' = 'week') {
  const anchor = new Date()
  if (mode === 'week') {
    const start = startOfWeek(anchor)
    const end = addDays(start, 7)
    return { start: start.toISOString(), end: end.toISOString() }
  }

  const start = startOfWeek(new Date(anchor.getFullYear(), anchor.getMonth(), 1))
  const end = addDays(startOfWeek(new Date(anchor.getFullYear(), anchor.getMonth() + 1, 1)), 7)
  return { start: start.toISOString(), end: end.toISOString() }
}

function baseMocks({ eventError = false } = {}): MockedResponse[] {
  const weekRange = currentRange('week')

  return [
    {
      request: { query: GET_FAMILIES },
      result: { data: { families: [family, otherFamily] } },
    },
    {
      request: { query: GET_FAMILY_MEMBERS, variables: { familyId: family.id } },
      result: { data: { familyMembers: [emma] } },
    },
    eventError
      ? {
          request: {
            query: GET_CALENDAR_EVENTS,
            variables: { familyId: family.id, rangeStart: weekRange.start, rangeEnd: weekRange.end },
          },
          error: new Error('Calendar unavailable'),
        }
      : {
          request: {
            query: GET_CALENDAR_EVENTS,
            variables: { familyId: family.id, rangeStart: weekRange.start, rangeEnd: weekRange.end },
          },
          result: { data: { calendarEvents: [calendarEvent] } },
        },
    {
      request: {
        query: GET_TASKS,
        variables: { boardId: family.boardId, includeCompleted: true },
      },
      result: { data: { tasks: [task] } },
    },
    {
      request: {
        query: GET_SCHEDULED_TASKS,
        variables: { familyId: family.id, rangeStart: weekRange.start, rangeEnd: weekRange.end },
      },
      result: { data: { scheduledTasks: [] } },
    },
  ]
}

function renderApp(mocks: MockedResponse[] = baseMocks()) {
  localStorage.clear()
  return render(
    <MockedProvider mocks={mocks}>
      <App />
    </MockedProvider>,
  )
}

describe('App', () => {
  it('renders loading state initially', () => {
    renderApp()

    expect(screen.getByText(/loading family calendar/i)).toBeInTheDocument()
  })

  it('renders the calendar in week view by default', async () => {
    renderApp()

    expect(await screen.findByRole('heading', { name: /smith family/i })).toBeInTheDocument()
    const nav = screen.getByRole('navigation')
    expect(within(nav).getByRole('button', { name: /calendar/i })).toHaveAttribute('aria-current', 'page')
    expect(screen.getByRole('button', { name: 'Week' })).toHaveClass('active')
    expect(await screen.findByText('Grocery Run')).toBeInTheDocument()
    expect(screen.getByText('Clean room')).toBeInTheDocument()
    expect(screen.getByText(/sat/i)).toBeInTheDocument()
  })

  it('switches to month view', async () => {
    const user = userEvent.setup()
    const monthRange = currentRange('month')
    renderApp([
      ...baseMocks(),
      {
        request: {
          query: GET_CALENDAR_EVENTS,
          variables: { familyId: family.id, rangeStart: monthRange.start, rangeEnd: monthRange.end },
        },
        result: { data: { calendarEvents: [calendarEvent] } },
      },
      {
        request: {
          query: GET_SCHEDULED_TASKS,
          variables: { familyId: family.id, rangeStart: monthRange.start, rangeEnd: monthRange.end },
        },
        result: { data: { scheduledTasks: [] } },
      },
    ])

    await screen.findByRole('heading', { name: /smith family/i })
    await user.click(screen.getByRole('button', { name: 'Month' }))

    expect(screen.getByRole('button', { name: 'Month' })).toHaveClass('active')
    expect(await screen.findByRole('region', { name: /month calendar/i })).toBeInTheDocument()
  })

  it('opens plus modal and creates a calendar event', async () => {
    const user = userEvent.setup()
    const weekRange = currentRange('week')
    const newEvent = {
      ...calendarEvent,
      id: 'event-2',
      title: 'Piano Lesson',
      startAt: buildDateTime(new Date(), '09:00').toISOString(),
      endAt: buildDateTime(new Date(), '10:00').toISOString(),
      notes: '',
    }

    renderApp([
      ...baseMocks(),
      {
        request: {
          query: CREATE_CALENDAR_EVENT,
          variables: {
            familyId: family.id,
            memberId: emma.id,
            title: 'Piano Lesson',
            startAt: newEvent.startAt,
            endAt: newEvent.endAt,
            notes: '',
          },
        },
        result: { data: { createCalendarEvent: newEvent } },
      },
      {
        request: {
          query: GET_CALENDAR_EVENTS,
          variables: { familyId: family.id, rangeStart: weekRange.start, rangeEnd: weekRange.end },
        },
        result: { data: { calendarEvents: [calendarEvent, newEvent] } },
      },
    ])

    await screen.findByRole('heading', { name: /smith family/i })
    await user.click(screen.getByRole('button', { name: /add calendar event/i }))

    expect(await screen.findByRole('dialog', { name: /new calendar event/i })).toBeInTheDocument()
    await user.type(screen.getByPlaceholderText(/guitar lesson/i), 'Piano Lesson')
    await user.click(screen.getByRole('button', { name: /create event/i }))

    expect(await screen.findByText('Piano Lesson')).toBeInTheDocument()
  })

  it('shows a newly-created event after switching to month view', async () => {
    const user = userEvent.setup()
    const weekRange = currentRange('week')
    const monthRange = currentRange('month')
    const newEvent = {
      ...calendarEvent,
      id: 'event-2',
      title: 'Piano Lesson',
      startAt: buildDateTime(new Date(), '09:00').toISOString(),
      endAt: buildDateTime(new Date(), '10:00').toISOString(),
      notes: '',
    }

    renderApp([
      ...baseMocks(),
      {
        request: {
          query: CREATE_CALENDAR_EVENT,
          variables: {
            familyId: family.id,
            memberId: emma.id,
            title: 'Piano Lesson',
            startAt: newEvent.startAt,
            endAt: newEvent.endAt,
            notes: '',
          },
        },
        result: { data: { createCalendarEvent: newEvent } },
      },
      {
        request: {
          query: GET_CALENDAR_EVENTS,
          variables: { familyId: family.id, rangeStart: weekRange.start, rangeEnd: weekRange.end },
        },
        result: { data: { calendarEvents: [calendarEvent, newEvent] } },
      },
      {
        request: {
          query: GET_CALENDAR_EVENTS,
          variables: { familyId: family.id, rangeStart: monthRange.start, rangeEnd: monthRange.end },
        },
        result: { data: { calendarEvents: [calendarEvent, newEvent] } },
      },
      {
        request: {
          query: GET_SCHEDULED_TASKS,
          variables: { familyId: family.id, rangeStart: monthRange.start, rangeEnd: monthRange.end },
        },
        result: { data: { scheduledTasks: [] } },
      },
    ])

    await screen.findByRole('heading', { name: /smith family/i })
    await user.click(screen.getByRole('button', { name: /add calendar event/i }))
    await user.type(screen.getByPlaceholderText(/guitar lesson/i), 'Piano Lesson')
    await user.click(screen.getByRole('button', { name: /create event/i }))
    await screen.findByText('Piano Lesson')
    await user.click(screen.getByRole('button', { name: 'Month' }))

    expect(await screen.findByRole('region', { name: /month calendar/i })).toBeInTheDocument()
    expect(await screen.findAllByText('Piano Lesson')).not.toHaveLength(0)
  })

  it('creates a scheduled chore and shows it on the calendar', async () => {
    const user = userEvent.setup()
    const weekRange = currentRange('week')
    const taskDate = formatDateInput(new Date())
    const dueAt = buildDateTime(new Date(), '09:00').toISOString()
    const scheduledTask = {
      ...task,
      id: 'task-2',
      title: 'Water plants',
      dueAt,
      durationMinutes: 60,
    }

    renderApp([
      ...baseMocks(),
      {
        request: {
          query: CREATE_TASK,
          variables: {
            title: 'Water plants',
            assigneeName: emma.name,
            familyId: family.id,
            boardId: family.boardId,
            status: 'TODO',
            dueAt,
            durationMinutes: 60,
            recurrenceRule: 'None',
          },
        },
        result: { data: { createTask: scheduledTask } },
      },
      {
        request: {
          query: GET_TASKS,
          variables: { boardId: family.boardId, includeCompleted: true },
        },
        result: { data: { tasks: [task, scheduledTask] } },
      },
      {
        request: {
          query: GET_SCHEDULED_TASKS,
          variables: { familyId: family.id, rangeStart: weekRange.start, rangeEnd: weekRange.end },
        },
        result: { data: { scheduledTasks: [scheduledTask] } },
      },
    ])

    await screen.findByRole('heading', { name: /smith family/i })
    await user.click(screen.getByRole('button', { name: /chores/i }))
    await user.type(screen.getByLabelText('Task title'), 'Water plants')
    await user.selectOptions(screen.getByLabelText('Assignee'), emma.name)
    await user.type(screen.getByLabelText('Chore date'), taskDate)
    await user.click(screen.getByRole('button', { name: /^add$/i }))
    await screen.findByText('Water plants')
    await user.click(screen.getByRole('button', { name: /calendar/i }))

    expect(await screen.findByText('Water plants')).toBeInTheDocument()
  })

  it('switches active family from the header', async () => {
    const user = userEvent.setup()
    renderApp([
      ...baseMocks(),
      {
        request: { query: GET_FAMILY_MEMBERS, variables: { familyId: otherFamily.id } },
        result: { data: { familyMembers: [] } },
      },
      {
        request: {
          query: GET_CALENDAR_EVENTS,
          variables: { familyId: otherFamily.id, rangeStart: currentRange('week').start, rangeEnd: currentRange('week').end },
        },
        result: { data: { calendarEvents: [] } },
      },
      {
        request: {
          query: GET_SCHEDULED_TASKS,
          variables: { familyId: otherFamily.id, rangeStart: currentRange('week').start, rangeEnd: currentRange('week').end },
        },
        result: { data: { scheduledTasks: [] } },
      },
      {
        request: { query: GET_TASKS, variables: { boardId: otherFamily.boardId, includeCompleted: true } },
        result: { data: { tasks: [] } },
      },
    ])

    await screen.findByRole('heading', { name: /smith family/i })
    await user.selectOptions(screen.getByLabelText('Family'), otherFamily.id)

    expect(await screen.findByRole('heading', { name: /garcia family/i })).toBeInTheDocument()
    expect(localStorage.getItem('todo-app-selected-family-id')).toBe(otherFamily.id)
  })

  it('renders settings forms for creating families and members', async () => {
    const user = userEvent.setup()
    renderApp()

    await screen.findByRole('heading', { name: /smith family/i })
    await user.click(screen.getByRole('button', { name: /settings/i }))

    expect(await screen.findByRole('heading', { name: /family app settings/i })).toBeInTheDocument()
    expect(screen.getByPlaceholderText(/garcia family/i)).toBeInTheDocument()
    expect(screen.getByPlaceholderText(/avery/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /create family/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /add member/i })).toBeInTheDocument()
  })

  it('opens event details with edit and delete controls', async () => {
    const user = userEvent.setup()
    renderApp()

    await screen.findByRole('heading', { name: /smith family/i })
    await user.click(await screen.findByText('Grocery Run'))

    expect(await screen.findByRole('dialog', { name: /edit calendar event/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /save changes/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /delete/i })).toBeInTheDocument()
  })

  it('keeps the calendar shell available when event sync fails', async () => {
    renderApp(baseMocks({ eventError: true }))

    expect(await screen.findByRole('heading', { name: /smith family/i })).toBeInTheDocument()
    expect(await screen.findByText(/sync is offline/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /retry sync/i })).toBeInTheDocument()
    expect(screen.getByText(/calendar unavailable/i)).toBeInTheDocument()
  })
})

function startOfWeek(date: Date) {
  const result = new Date(date)
  result.setHours(0, 0, 0, 0)
  result.setDate(result.getDate() - result.getDay())
  return result
}

function addDays(date: Date, days: number) {
  const result = new Date(date)
  result.setDate(result.getDate() + days)
  return result
}

function formatDateInput(date: Date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}

function buildDateTime(date: Date, time: string) {
  return new Date(`${formatDateInput(date)}T${time}:00`)
}
