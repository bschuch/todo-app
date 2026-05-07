import { gql } from '@apollo/client'
import { useApolloClient, useMutation, useQuery } from '@apollo/client/react'
import { type CSSProperties, type FormEvent, type ReactNode, useEffect, useMemo, useState } from 'react'
import './App.css'

type TaskStatus = 'TODO' | 'IN_PROGRESS' | 'DONE'
type AppSection = 'Calendar' | 'Chores' | 'Rewards' | 'Meals' | 'Photos' | 'Lists' | 'Sleep' | 'Settings'
type CalendarMode = 'week' | 'month'
type AuthMode = 'signin' | 'signup'

interface Task {
  id: string
  familyId?: string | null
  title: string
  assigneeName: string
  status: TaskStatus
  completed: boolean
  sortOrder: number
  dueAt?: string | null
  durationMinutes?: number | null
  recurrenceRule?: string
  updatedAt: string
}

interface Family {
  id: string
  name: string
  boardId: string
}

interface FamilyMember {
  id: string
  familyId: string
  name: string
  color: string
}

interface AppUser {
  id: string
  email: string
  displayName: string
}

interface CurrentUserQueryData {
  currentUser: AppUser | null
}

interface AuthPayload {
  user: AppUser
  token: string
}

interface CalendarEvent {
  id: string
  familyId: string
  memberId?: string | null
  title: string
  startAt: string
  endAt: string
  notes: string
  tone: string
}

interface FamilyInvite {
  id: string
  familyId: string
  code: string
  expiresAt: string
}

interface TasksQueryData {
  tasks: Task[]
}

interface ScheduledTasksQueryData {
  scheduledTasks: Task[]
}

interface FamiliesQueryData {
  families: Family[]
}

interface FamilyMembersQueryData {
  familyMembers: FamilyMember[]
}

interface CalendarEventsQueryData {
  calendarEvents: CalendarEvent[]
}

interface MemberSummary {
  id?: string
  name: string
  count: number
  completed: number
  color: string
}

interface EventFormState {
  title: string
  date: string
  startTime: string
  endTime: string
  memberId: string
  notes: string
}

interface TaskScheduleFormState {
  date: string
  time: string
  durationMinutes: string
  recurrenceRule: string
}

const BOARD_ID = 'family-home'
const SELECTED_FAMILY_KEY = 'todo-app-selected-family-id'
const SESSION_TOKEN_KEY = 'todo-app-session-token'

const STATUS_COLUMNS: Array<{ id: TaskStatus; title: string; description: string }> = [
  { id: 'TODO', title: 'To do', description: 'Ready to be picked up next.' },
  { id: 'IN_PROGRESS', title: 'In progress', description: 'Currently being worked through.' },
  { id: 'DONE', title: 'Done', description: 'Completed and ready to archive later.' },
]

const PERSON_COLORS = ['#6dbec2', '#f2a7b2', '#b89acb', '#d67268', '#a7d6a1', '#efc55d']
const MEMBER_NAMES = ['Dad', 'Ella', 'Harper', 'Mom', 'Liam']
const TIME_SLOTS = [8, 9, 10, 11, 12, 13, 14, 15, 16, 17]

const APP_SECTIONS: Array<{ id: AppSection; label: string; icon: string }> = [
  { id: 'Calendar', label: 'Calendar', icon: 'Cal' },
  { id: 'Chores', label: 'Chores', icon: 'Ch' },
  { id: 'Rewards', label: 'Rewards', icon: 'Rw' },
  { id: 'Meals', label: 'Meals', icon: 'Ml' },
  { id: 'Photos', label: 'Photos', icon: 'Ph' },
  { id: 'Lists', label: 'Lists', icon: 'Li' },
  { id: 'Sleep', label: 'Sleep', icon: 'Sl' },
  { id: 'Settings', label: 'Settings', icon: 'St' },
]

export const GET_CURRENT_USER = gql`
  query GetCurrentUser {
    currentUser {
      id
      email
      displayName
    }
  }
`

export const GET_FAMILIES = gql`
  query GetFamilies {
    families {
      id
      name
      boardId
    }
  }
`

export const SIGN_UP = gql`
  mutation SignUp($email: String!, $displayName: String!, $password: String!) {
    signUp(email: $email, displayName: $displayName, password: $password) {
      token
      user {
        id
        email
        displayName
      }
    }
  }
`

export const SIGN_IN = gql`
  mutation SignIn($email: String!, $password: String!) {
    signIn(email: $email, password: $password) {
      token
      user {
        id
        email
        displayName
      }
    }
  }
`

export const CREATE_FAMILY_INVITE = gql`
  mutation CreateFamilyInvite($familyId: String!) {
    createFamilyInvite(familyId: $familyId) {
      id
      familyId
      code
      expiresAt
    }
  }
`

export const ACCEPT_FAMILY_INVITE = gql`
  mutation AcceptFamilyInvite($code: String!) {
    acceptFamilyInvite(code: $code) {
      id
      name
      boardId
    }
  }
`

export const GET_FAMILY_MEMBERS = gql`
  query GetFamilyMembers($familyId: String!) {
    familyMembers(familyId: $familyId) {
      id
      familyId
      name
      color
    }
  }
`

export const GET_CALENDAR_EVENTS = gql`
  query GetCalendarEvents($familyId: String!, $rangeStart: DateTime!, $rangeEnd: DateTime!) {
    calendarEvents(familyId: $familyId, rangeStart: $rangeStart, rangeEnd: $rangeEnd) {
      id
      familyId
      memberId
      title
      startAt
      endAt
      notes
      tone
    }
  }
`

export const GET_SCHEDULED_TASKS = gql`
  query GetScheduledTasks($familyId: String!, $rangeStart: DateTime!, $rangeEnd: DateTime!) {
    scheduledTasks(familyId: $familyId, rangeStart: $rangeStart, rangeEnd: $rangeEnd) {
      id
      familyId
      title
      assigneeName
      status
      completed
      sortOrder
      dueAt
      durationMinutes
      recurrenceRule
      updatedAt
    }
  }
`

export const GET_TASKS = gql`
  query GetTasks($boardId: String!, $includeCompleted: Boolean!) {
    tasks(boardId: $boardId, includeCompleted: $includeCompleted) {
      id
      familyId
      title
      assigneeName
      status
      completed
      sortOrder
      dueAt
      durationMinutes
      recurrenceRule
      updatedAt
    }
  }
`

const CREATE_FAMILY = gql`
  mutation CreateFamily($name: String!) {
    createFamily(name: $name) {
      id
      name
      boardId
    }
  }
`

const CREATE_FAMILY_MEMBER = gql`
  mutation CreateFamilyMember($familyId: String!, $name: String!, $color: String!) {
    createFamilyMember(familyId: $familyId, name: $name, color: $color) {
      id
      familyId
      name
      color
    }
  }
`

const UPDATE_FAMILY_MEMBER = gql`
  mutation UpdateFamilyMember($memberId: String!, $name: String!, $color: String!) {
    updateFamilyMember(memberId: $memberId, name: $name, color: $color) {
      id
      familyId
      name
      color
    }
  }
`

export const CREATE_CALENDAR_EVENT = gql`
  mutation CreateCalendarEvent(
    $familyId: String!
    $memberId: String
    $title: String!
    $startAt: DateTime!
    $endAt: DateTime!
    $notes: String
  ) {
    createCalendarEvent(
      familyId: $familyId
      memberId: $memberId
      title: $title
      startAt: $startAt
      endAt: $endAt
      notes: $notes
    ) {
      id
      familyId
      memberId
      title
      startAt
      endAt
      notes
      tone
    }
  }
`

const UPDATE_CALENDAR_EVENT = gql`
  mutation UpdateCalendarEvent(
    $eventId: String!
    $memberId: String
    $title: String!
    $startAt: DateTime!
    $endAt: DateTime!
    $notes: String
  ) {
    updateCalendarEvent(
      eventId: $eventId
      memberId: $memberId
      title: $title
      startAt: $startAt
      endAt: $endAt
      notes: $notes
    ) {
      id
      familyId
      memberId
      title
      startAt
      endAt
      notes
      tone
    }
  }
`

const DELETE_CALENDAR_EVENT = gql`
  mutation DeleteCalendarEvent($eventId: String!) {
    deleteCalendarEvent(eventId: $eventId)
  }
`

export const CREATE_TASK = gql`
  mutation CreateTask(
    $title: String!
    $assigneeName: String!
    $familyId: String
    $boardId: String!
    $status: TaskStatus!
    $dueAt: DateTime
    $durationMinutes: Int
    $recurrenceRule: String
  ) {
    createTask(
      title: $title
      assigneeName: $assigneeName
      familyId: $familyId
      boardId: $boardId
      status: $status
      dueAt: $dueAt
      durationMinutes: $durationMinutes
      recurrenceRule: $recurrenceRule
    ) {
      id
      familyId
      title
      assigneeName
      status
      completed
      sortOrder
      dueAt
      durationMinutes
      recurrenceRule
      updatedAt
    }
  }
`

const MOVE_TASK = gql`
  mutation MoveTask($taskId: String!, $targetStatus: TaskStatus!, $targetOrder: Int!) {
    moveTask(taskId: $taskId, targetStatus: $targetStatus, targetOrder: $targetOrder) {
      id
      status
      completed
      sortOrder
      updatedAt
    }
  }
`

const TOGGLE_TASK_COMPLETION = gql`
  mutation ToggleTaskCompletion($taskId: String!) {
    toggleTaskCompletion(taskId: $taskId) {
      id
      status
      completed
      sortOrder
      updatedAt
    }
  }
`

const DELETE_TASK = gql`
  mutation DeleteTask($taskId: String!) {
    deleteTask(taskId: $taskId)
  }
`

function App() {
  const apolloClient = useApolloClient()
  const [activeSection, setActiveSection] = useState<AppSection>('Calendar')
  const [calendarMode, setCalendarMode] = useState<CalendarMode>('week')
  const [anchorDate, setAnchorDate] = useState(() => new Date())
  const [selectedFamilyId, setSelectedFamilyId] = useState(() => localStorage.getItem(SELECTED_FAMILY_KEY) ?? '')
  const [sessionToken, setSessionToken] = useState(() => localStorage.getItem(SESSION_TOKEN_KEY) ?? '')
  const [authMode, setAuthMode] = useState<AuthMode>('signin')
  const [authEmail, setAuthEmail] = useState('')
  const [authDisplayName, setAuthDisplayName] = useState('')
  const [authPassword, setAuthPassword] = useState('')
  const [inviteCode, setInviteCode] = useState('')
  const [latestInvite, setLatestInvite] = useState<FamilyInvite | null>(null)
  const [title, setTitle] = useState('')
  const [assigneeName, setAssigneeName] = useState('')
  const [taskScheduleForm, setTaskScheduleForm] = useState<TaskScheduleFormState>(() => createDefaultTaskScheduleForm(new Date()))
  const [hideCompleted, setHideCompleted] = useState(false)
  const [draggingTaskId, setDraggingTaskId] = useState<string | null>(null)
  const [activeDropKey, setActiveDropKey] = useState<string | null>(null)
  const [isEventModalOpen, setIsEventModalOpen] = useState(false)
  const [editingEvent, setEditingEvent] = useState<CalendarEvent | null>(null)
  const [selectedTask, setSelectedTask] = useState<Task | null>(null)
  const [eventForm, setEventForm] = useState<EventFormState>(() => createDefaultEventForm(new Date()))
  const [newFamilyName, setNewFamilyName] = useState('')
  const [newMemberName, setNewMemberName] = useState('')
  const [newMemberColor, setNewMemberColor] = useState(PERSON_COLORS[0])
  const [memberDrafts, setMemberDrafts] = useState<Record<string, { name: string; color: string }>>({})

  const visibleRange = useMemo(() => getVisibleRange(anchorDate, calendarMode), [anchorDate, calendarMode])

  const {
    loading: loadingCurrentUser,
    error: currentUserError,
    data: currentUserData,
    refetch: refetchCurrentUser,
  } = useQuery<CurrentUserQueryData>(GET_CURRENT_USER)

  const currentUser = currentUserData?.currentUser ?? null

  const {
    loading: loadingFamilies,
    error: familyError,
    data: familiesData,
    refetch: refetchFamilies,
  } = useQuery<FamiliesQueryData>(GET_FAMILIES)

  const families = useMemo(() => familiesData?.families ?? [], [familiesData])
  const selectedFamily = useMemo(
    () => families.find((family) => family.id === selectedFamilyId) ?? families[0],
    [families, selectedFamilyId],
  )
  const activeFamilyId = selectedFamily?.id ?? ''
  const activeBoardId = selectedFamily?.boardId ?? BOARD_ID

  useEffect(() => {
    if (!selectedFamily && families.length > 0) {
      setSelectedFamilyId(families[0].id)
      return
    }

    if (selectedFamily) {
      localStorage.setItem(SELECTED_FAMILY_KEY, selectedFamily.id)
    }
  }, [families, selectedFamily])

  const {
    error: membersError,
    data: membersData,
    refetch: refetchMembers,
  } = useQuery<FamilyMembersQueryData>(GET_FAMILY_MEMBERS, {
    skip: !activeFamilyId,
    variables: { familyId: activeFamilyId },
  })

  const {
    loading: loadingEvents,
    error: eventsError,
    data: eventsData,
    refetch: refetchEvents,
  } = useQuery<CalendarEventsQueryData>(GET_CALENDAR_EVENTS, {
    skip: !activeFamilyId,
    variables: {
      familyId: activeFamilyId,
      rangeStart: visibleRange.start.toISOString(),
      rangeEnd: visibleRange.end.toISOString(),
    },
  })

  const {
    error: scheduledTasksError,
    data: scheduledTasksData,
    refetch: refetchScheduledTasks,
  } = useQuery<ScheduledTasksQueryData>(GET_SCHEDULED_TASKS, {
    skip: !activeFamilyId,
    variables: {
      familyId: activeFamilyId,
      rangeStart: visibleRange.start.toISOString(),
      rangeEnd: visibleRange.end.toISOString(),
    },
  })

  const queryVariables = {
    boardId: activeBoardId,
    includeCompleted: !hideCompleted,
  }

  const {
    loading: loadingTasks,
    error: tasksError,
    data: tasksData,
    refetch: refetchTasks,
  } = useQuery<TasksQueryData>(GET_TASKS, {
    variables: queryVariables,
  })

  const members = useMemo(() => membersData?.familyMembers ?? [], [membersData])
  const events = useMemo(() => eventsData?.calendarEvents ?? [], [eventsData])
  const tasks = useMemo(() => tasksData?.tasks ?? [], [tasksData])
  const scheduledTasks = useMemo(() => scheduledTasksData?.scheduledTasks ?? [], [scheduledTasksData])

  useEffect(() => {
    if (members.length > 0) {
      setAssigneeName((current) => current || members[0].name)
      setEventForm((current) => ({ ...current, memberId: current.memberId || members[0].id }))
      setMemberDrafts((current) => {
        const next = { ...current }
        members.forEach((member) => {
          next[member.id] ??= { name: member.name, color: member.color }
        })
        return next
      })
    }
  }, [members])

  const memberSummaries = useMemo<MemberSummary[]>(() => {
    const taskMap = new Map<string, { count: number; completed: number }>()
    tasks.forEach((task) => {
      const key = task.assigneeName || 'Unassigned'
      const current = taskMap.get(key) ?? { count: 0, completed: 0 }
      current.count += 1
      current.completed += task.completed ? 1 : 0
      taskMap.set(key, current)
    })

    if (members.length > 0) {
      return members.map((member) => {
        const taskStats = taskMap.get(member.name) ?? { count: 0, completed: 0 }
        return {
          id: member.id,
          name: member.name,
          count: taskStats.count,
          completed: taskStats.completed,
          color: member.color,
        }
      })
    }

    if (taskMap.size > 0) {
      return [...taskMap.entries()].map(([name, stats], index) => ({
        name,
        count: stats.count,
        completed: stats.completed,
        color: PERSON_COLORS[index % PERSON_COLORS.length],
      }))
    }

    return MEMBER_NAMES.slice(0, 4).map((name, index) => ({
      name,
      count: 0,
      completed: 0,
      color: PERSON_COLORS[index],
    }))
  }, [members, tasks])

  const taskColorMap = useMemo(() => {
    return memberSummaries.reduce<Record<string, string>>((result, member) => {
      result[member.name] = member.color
      return result
    }, {})
  }, [memberSummaries])

  const memberMap = useMemo(() => {
    return members.reduce<Record<string, FamilyMember>>((result, member) => {
      result[member.id] = member
      return result
    }, {})
  }, [members])

  const groupedColumns = useMemo(
    () =>
      STATUS_COLUMNS.map((column) => ({
        ...column,
        tasks: tasks.filter((task) => task.status === column.id).sort((left, right) => left.sortOrder - right.sortOrder),
      })),
    [tasks],
  )

  const [signUp, { loading: signingUp }] = useMutation(SIGN_UP)
  const [signIn, { loading: signingIn }] = useMutation(SIGN_IN)

  const [createFamily, { loading: creatingFamily }] = useMutation(CREATE_FAMILY, {
    refetchQueries: [{ query: GET_FAMILIES }],
    awaitRefetchQueries: true,
  })

  const [createFamilyMember, { loading: creatingMember }] = useMutation(CREATE_FAMILY_MEMBER, {
    refetchQueries: activeFamilyId ? [{ query: GET_FAMILY_MEMBERS, variables: { familyId: activeFamilyId } }] : [],
    awaitRefetchQueries: true,
  })

  const [updateFamilyMember, { loading: updatingMember }] = useMutation(UPDATE_FAMILY_MEMBER, {
    refetchQueries: activeFamilyId ? [{ query: GET_FAMILY_MEMBERS, variables: { familyId: activeFamilyId } }] : [],
    awaitRefetchQueries: true,
  })

  const [createFamilyInvite, { loading: creatingInvite }] = useMutation(CREATE_FAMILY_INVITE)
  const [acceptFamilyInvite, { loading: acceptingInvite }] = useMutation(ACCEPT_FAMILY_INVITE, {
    refetchQueries: [{ query: GET_FAMILIES }],
    awaitRefetchQueries: true,
  })

  const [createCalendarEvent, { loading: creatingEvent }] = useMutation(CREATE_CALENDAR_EVENT, {
    refetchQueries: calendarRefetchQueries(activeFamilyId, visibleRange),
    awaitRefetchQueries: true,
  })

  const [updateCalendarEvent, { loading: updatingEvent }] = useMutation(UPDATE_CALENDAR_EVENT, {
    refetchQueries: calendarRefetchQueries(activeFamilyId, visibleRange),
    awaitRefetchQueries: true,
  })

  const [deleteCalendarEvent, { loading: deletingEvent }] = useMutation(DELETE_CALENDAR_EVENT, {
    refetchQueries: calendarRefetchQueries(activeFamilyId, visibleRange),
    awaitRefetchQueries: true,
  })

  const [createTask, { loading: creatingTask }] = useMutation(CREATE_TASK, {
    refetchQueries: taskRefetchQueries(queryVariables, activeFamilyId, visibleRange),
    awaitRefetchQueries: true,
  })

  const [moveTask, { loading: movingTask }] = useMutation(MOVE_TASK, {
    refetchQueries: [{ query: GET_TASKS, variables: queryVariables }],
    awaitRefetchQueries: true,
  })

  const [toggleTaskCompletion, { loading: togglingTask }] = useMutation(TOGGLE_TASK_COMPLETION, {
    refetchQueries: [{ query: GET_TASKS, variables: queryVariables }],
    awaitRefetchQueries: true,
  })

  const [deleteTask, { loading: deletingTask }] = useMutation(DELETE_TASK, {
    refetchQueries: [{ query: GET_TASKS, variables: queryVariables }],
    awaitRefetchQueries: true,
  })

  const pendingMutation =
    creatingTask ||
    movingTask ||
    togglingTask ||
    deletingTask ||
    creatingEvent ||
    updatingEvent ||
    deletingEvent ||
    creatingFamily ||
    creatingMember ||
    updatingMember
  const taskSyncError = tasksError?.message ?? null
  const authSyncError = currentUserError?.message ?? null
  const appSyncError = authSyncError ?? familyError?.message ?? membersError?.message ?? eventsError?.message ?? scheduledTasksError?.message ?? taskSyncError

  const handleAuthSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!authEmail.trim() || !authPassword.trim() || (authMode === 'signup' && !authDisplayName.trim())) {
      return
    }

    const result =
      authMode === 'signup'
        ? await signUp({ variables: { email: authEmail, displayName: authDisplayName, password: authPassword } })
        : await signIn({ variables: { email: authEmail, password: authPassword } })
    const payload = (result.data as { signUp?: AuthPayload; signIn?: AuthPayload } | undefined)?.signUp ?? (result.data as { signIn?: AuthPayload } | undefined)?.signIn

    if (payload?.token) {
      localStorage.setItem(SESSION_TOKEN_KEY, payload.token)
      setSessionToken(payload.token)
      setAuthPassword('')
      await apolloClient.resetStore()
    }
  }

  const handleSignOut = async () => {
    localStorage.removeItem(SESSION_TOKEN_KEY)
    setSessionToken('')
    setSelectedFamilyId('')
    localStorage.removeItem(SELECTED_FAMILY_KEY)
    await apolloClient.resetStore()
  }

  const handleSelectFamily = (familyId: string) => {
    setSelectedFamilyId(familyId)
    localStorage.setItem(SELECTED_FAMILY_KEY, familyId)
  }

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!title.trim()) {
      return
    }

    await createTask({
      variables: {
        title,
        assigneeName: assigneeName || members[0]?.name || 'Unassigned',
        familyId: activeFamilyId || null,
        boardId: activeBoardId,
        status: 'TODO',
        dueAt: taskScheduleForm.date ? buildDateTime(taskScheduleForm.date, taskScheduleForm.time || '09:00').toISOString() : null,
        durationMinutes: taskScheduleForm.date ? Number(taskScheduleForm.durationMinutes) || 60 : null,
        recurrenceRule: taskScheduleForm.recurrenceRule,
      },
    })

    setTitle('')
    setTaskScheduleForm(createDefaultTaskScheduleForm(new Date()))
  }

  const handleMoveTask = async (targetStatus: TaskStatus, targetOrder: number) => {
    if (!draggingTaskId || pendingMutation) {
      return
    }

    setActiveDropKey(null)

    await moveTask({
      variables: {
        taskId: draggingTaskId,
        targetStatus,
        targetOrder,
      },
    })

    setDraggingTaskId(null)
  }

  const handleToggleTask = async (taskId: string) => {
    await toggleTaskCompletion({ variables: { taskId } })
  }

  const handleDeleteTask = async (taskId: string) => {
    await deleteTask({ variables: { taskId } })
    setSelectedTask(null)
  }

  const handleOpenNewEvent = (date = anchorDate) => {
    setEditingEvent(null)
    setEventForm(createDefaultEventForm(date, members[0]?.id))
    setIsEventModalOpen(true)
  }

  const handleOpenEventDetails = (calendarEvent: CalendarEvent) => {
    setEditingEvent(calendarEvent)
    setEventForm(createEventFormFromEvent(calendarEvent))
    setIsEventModalOpen(true)
  }

  const handleSaveEvent = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!activeFamilyId || !eventForm.title.trim()) {
      return
    }

    const variables = {
      memberId: eventForm.memberId || null,
      title: eventForm.title,
      startAt: buildDateTime(eventForm.date, eventForm.startTime).toISOString(),
      endAt: buildDateTime(eventForm.date, eventForm.endTime).toISOString(),
      notes: eventForm.notes,
    }

    if (editingEvent) {
      await updateCalendarEvent({
        variables: {
          eventId: editingEvent.id,
          ...variables,
        },
      })
    } else {
      await createCalendarEvent({
        variables: {
          familyId: activeFamilyId,
          ...variables,
        },
      })
    }

    setIsEventModalOpen(false)
    setEditingEvent(null)
  }

  const handleDeleteEvent = async () => {
    if (!editingEvent) {
      return
    }

    await deleteCalendarEvent({ variables: { eventId: editingEvent.id } })
    setIsEventModalOpen(false)
    setEditingEvent(null)
  }

  const handleCreateFamily = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!newFamilyName.trim()) {
      return
    }

    const result = await createFamily({ variables: { name: newFamilyName } })
    const createdFamily = (result.data as { createFamily?: Family } | undefined)?.createFamily
    if (createdFamily) {
      handleSelectFamily(createdFamily.id)
    }
    setNewFamilyName('')
    void refetchFamilies()
  }

  const handleCreateMember = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!activeFamilyId || !newMemberName.trim()) {
      return
    }

    await createFamilyMember({
      variables: {
        familyId: activeFamilyId,
        name: newMemberName,
        color: newMemberColor,
      },
    })

    setNewMemberName('')
    setNewMemberColor(PERSON_COLORS[(members.length + 1) % PERSON_COLORS.length])
  }

  const handleUpdateMember = async (member: FamilyMember) => {
    const draft = memberDrafts[member.id]
    if (!draft || !draft.name.trim()) {
      return
    }

    await updateFamilyMember({
      variables: {
        memberId: member.id,
        name: draft.name,
        color: draft.color,
      },
    })
  }

  const retrySync = () => {
    void refetchCurrentUser()
    void refetchFamilies()
    void refetchMembers()
    void refetchEvents()
    void refetchScheduledTasks()
    void refetchTasks()
  }

  const dateLabel = new Intl.DateTimeFormat('en-US', {
    weekday: 'long',
    month: 'long',
    day: 'numeric',
  }).format(new Date())

  const timeLabel = new Intl.DateTimeFormat('en-US', {
    hour: 'numeric',
    minute: '2-digit',
  }).format(new Date())

  if (loadingFamilies) {
    return (
      <div className="app-shell">
        <div className="state-panel loading-state">
          <div>
            <h1>Loading family calendar...</h1>
            <p>Pulling families, members, tasks, and events from the GraphQL API.</p>
          </div>
        </div>
      </div>
    )
  }

  return (
    <div className="app-shell">
      <main className="family-app" aria-label="Family organizer">
        <Navigation activeSection={activeSection} onSelectSection={setActiveSection} />

        <section className="content-panel">
          {appSyncError ? <SyncBanner message={appSyncError} onRetry={retrySync} /> : null}
          <AccessBar
            acceptingInvite={acceptingInvite}
            authDisplayName={authDisplayName}
            authEmail={authEmail}
            authMode={authMode}
            authPassword={authPassword}
            currentUser={currentUser}
            hasSession={Boolean(sessionToken)}
            inviteCode={inviteCode}
            loadingCurrentUser={loadingCurrentUser}
            onAcceptInvite={async (event) => {
              event.preventDefault()
              if (!inviteCode.trim()) {
                return
              }
              const result = await acceptFamilyInvite({ variables: { code: inviteCode } })
              const family = (result.data as { acceptFamilyInvite?: Family } | undefined)?.acceptFamilyInvite
              if (family) {
                handleSelectFamily(family.id)
              }
              setInviteCode('')
            }}
            onAuthSubmit={handleAuthSubmit}
            onSignOut={handleSignOut}
            setAuthDisplayName={setAuthDisplayName}
            setAuthEmail={setAuthEmail}
            setAuthMode={setAuthMode}
            setAuthPassword={setAuthPassword}
            setInviteCode={setInviteCode}
            signingIn={signingIn}
            signingUp={signingUp}
          />

          {activeSection === 'Calendar' ? (
            <CalendarView
              anchorDate={anchorDate}
              calendarMode={calendarMode}
              dateLabel={dateLabel}
              events={events}
              family={selectedFamily}
              families={families}
              loadingEvents={loadingEvents}
              memberMap={memberMap}
              memberSummaries={memberSummaries}
              members={members}
              onChangeMode={setCalendarMode}
              onChangeFamily={handleSelectFamily}
              onOpenEvent={handleOpenEventDetails}
              onOpenNewEvent={handleOpenNewEvent}
              onOpenTask={setSelectedTask}
              onShiftDate={(direction) => setAnchorDate((current) => shiftDate(current, calendarMode, direction))}
              scheduledTasks={scheduledTasks}
              taskColorMap={taskColorMap}
              tasks={tasks}
              timeLabel={timeLabel}
            />
          ) : null}

          {activeSection === 'Chores' ? (
            <ChoresView
              activeDropKey={activeDropKey}
              assigneeName={assigneeName}
              creatingTask={creatingTask}
              dateLabel={dateLabel}
              draggingTaskId={draggingTaskId}
              family={selectedFamily}
              groupedColumns={groupedColumns}
              handleDeleteTask={handleDeleteTask}
              handleMoveTask={handleMoveTask}
              handleSubmit={handleSubmit}
              handleToggleTask={handleToggleTask}
              hideCompleted={hideCompleted}
              loadingTasks={loadingTasks}
              memberSummaries={memberSummaries}
              pendingMutation={pendingMutation}
              refetch={refetchTasks}
              setActiveDropKey={setActiveDropKey}
              setAssigneeName={setAssigneeName}
              setDraggingTaskId={setDraggingTaskId}
              setHideCompleted={setHideCompleted}
              setTaskScheduleForm={setTaskScheduleForm}
              setTitle={setTitle}
              taskScheduleForm={taskScheduleForm}
              taskColorMap={taskColorMap}
              taskSyncError={taskSyncError}
              tasks={tasks}
              title={title}
            />
          ) : null}

          {activeSection === 'Rewards' ? <RewardsView memberSummaries={memberSummaries} /> : null}
          {activeSection === 'Meals' ? <MealsView /> : null}
          {activeSection === 'Photos' ? <PhotosView /> : null}
          {activeSection === 'Lists' ? <ListsView tasks={tasks} /> : null}
          {activeSection === 'Sleep' ? <SleepView /> : null}
          {activeSection === 'Settings' ? (
            <SettingsView
              creatingFamily={creatingFamily}
              creatingMember={creatingMember}
              families={families}
              family={selectedFamily}
              handleCreateFamily={handleCreateFamily}
              handleCreateMember={handleCreateMember}
              handleUpdateMember={handleUpdateMember}
              memberDrafts={memberDrafts}
              members={members}
              newFamilyName={newFamilyName}
              newMemberColor={newMemberColor}
              newMemberName={newMemberName}
              onChangeFamily={handleSelectFamily}
              creatingInvite={creatingInvite}
              latestInvite={latestInvite}
              handleCreateInvite={async () => {
                if (!activeFamilyId) {
                  return
                }
                const result = await createFamilyInvite({ variables: { familyId: activeFamilyId } })
                const invite = (result.data as { createFamilyInvite?: FamilyInvite } | undefined)?.createFamilyInvite
                if (invite) {
                  setLatestInvite(invite)
                }
              }}
              setMemberDrafts={setMemberDrafts}
              setNewFamilyName={setNewFamilyName}
              setNewMemberColor={setNewMemberColor}
              setNewMemberName={setNewMemberName}
              updatingMember={updatingMember}
            />
          ) : null}
        </section>
      </main>

      {isEventModalOpen ? (
        <EventModal
          deletingEvent={deletingEvent}
          editingEvent={editingEvent}
          eventForm={eventForm}
          members={members}
          onClose={() => {
            setIsEventModalOpen(false)
            setEditingEvent(null)
          }}
          onDelete={handleDeleteEvent}
          onSave={handleSaveEvent}
          setEventForm={setEventForm}
          savingEvent={creatingEvent || updatingEvent}
        />
      ) : null}

      {selectedTask ? (
        <TaskDetailModal
          pendingMutation={pendingMutation}
          task={selectedTask}
          taskColorMap={taskColorMap}
          onClose={() => setSelectedTask(null)}
          onDelete={handleDeleteTask}
          onToggle={handleToggleTask}
        />
      ) : null}
    </div>
  )
}

function SyncBanner({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <aside className="sync-banner" role="status">
      <div>
        <strong>Sync is offline</strong>
        <span>{message}. The calendar shell stays available while sync recovers.</span>
      </div>
      <button className="ghost-button" type="button" onClick={onRetry}>
        Retry sync
      </button>
    </aside>
  )
}

function AccessBar({
  acceptingInvite,
  authDisplayName,
  authEmail,
  authMode,
  authPassword,
  currentUser,
  hasSession,
  inviteCode,
  loadingCurrentUser,
  onAcceptInvite,
  onAuthSubmit,
  onSignOut,
  setAuthDisplayName,
  setAuthEmail,
  setAuthMode,
  setAuthPassword,
  setInviteCode,
  signingIn,
  signingUp,
}: {
  acceptingInvite: boolean
  authDisplayName: string
  authEmail: string
  authMode: AuthMode
  authPassword: string
  currentUser: AppUser | null
  hasSession: boolean
  inviteCode: string
  loadingCurrentUser: boolean
  onAcceptInvite: (event: FormEvent<HTMLFormElement>) => Promise<void>
  onAuthSubmit: (event: FormEvent<HTMLFormElement>) => Promise<void>
  onSignOut: () => Promise<void>
  setAuthDisplayName: (value: string) => void
  setAuthEmail: (value: string) => void
  setAuthMode: (mode: AuthMode) => void
  setAuthPassword: (value: string) => void
  setInviteCode: (value: string) => void
  signingIn: boolean
  signingUp: boolean
}) {
  const isSigning = signingIn || signingUp

  return (
    <section className="access-bar" aria-label="Family access">
      <div>
        <span className="board-kicker">Private access</span>
        <strong>{currentUser ? `Signed in as ${currentUser.displayName}` : hasSession || loadingCurrentUser ? 'Checking access...' : 'Demo mode or sign in'}</strong>
      </div>

      {currentUser ? (
        <div className="access-actions">
          <form className="invite-accept-form" onSubmit={(event) => void onAcceptInvite(event)}>
            <input value={inviteCode} onChange={(event) => setInviteCode(event.target.value)} placeholder="Invite code" aria-label="Invite code" />
            <button className="ghost-button" type="submit" disabled={acceptingInvite}>
              {acceptingInvite ? 'Joining...' : 'Join family'}
            </button>
          </form>
          <button className="ghost-button" type="button" onClick={() => void onSignOut()}>
            Sign out
          </button>
        </div>
      ) : (
        <form className="auth-form" onSubmit={(event) => void onAuthSubmit(event)}>
          <div className="segmented-control" aria-label="Access mode">
            <button className={authMode === 'signin' ? 'active' : ''} type="button" onClick={() => setAuthMode('signin')}>
              Sign in
            </button>
            <button className={authMode === 'signup' ? 'active' : ''} type="button" onClick={() => setAuthMode('signup')}>
              Sign up
            </button>
          </div>
          <input value={authEmail} onChange={(event) => setAuthEmail(event.target.value)} placeholder="Email" aria-label="Email" type="email" />
          {authMode === 'signup' ? (
            <input value={authDisplayName} onChange={(event) => setAuthDisplayName(event.target.value)} placeholder="Display name" aria-label="Display name" />
          ) : null}
          <input value={authPassword} onChange={(event) => setAuthPassword(event.target.value)} placeholder="Password" aria-label="Password" type="password" />
          <button className="primary-button" type="submit" disabled={isSigning}>
            {isSigning ? 'Working...' : authMode === 'signup' ? 'Create account' : 'Sign in'}
          </button>
        </form>
      )}
    </section>
  )
}

function Navigation({
  activeSection,
  onSelectSection,
}: {
  activeSection: AppSection
  onSelectSection: (section: AppSection) => void
}) {
  return (
    <aside className="side-nav" aria-label="App sections">
      <div className="brand-mark">S</div>
      <nav className="nav-menu">
        {APP_SECTIONS.map((section) => (
          <button
            className={`nav-item ${activeSection === section.id ? 'active' : ''}`}
            key={section.id}
            type="button"
            onClick={() => onSelectSection(section.id)}
            aria-current={activeSection === section.id ? 'page' : undefined}
          >
            <span className="nav-icon" aria-hidden="true">
              {section.icon}
            </span>
            <span>{section.label}</span>
          </button>
        ))}
      </nav>
    </aside>
  )
}

function CalendarView({
  anchorDate,
  calendarMode,
  dateLabel,
  events,
  family,
  families,
  loadingEvents,
  memberMap,
  memberSummaries,
  members,
  onChangeMode,
  onChangeFamily,
  onOpenEvent,
  onOpenNewEvent,
  onOpenTask,
  onShiftDate,
  scheduledTasks,
  taskColorMap,
  tasks,
  timeLabel,
}: {
  anchorDate: Date
  calendarMode: CalendarMode
  dateLabel: string
  events: CalendarEvent[]
  family?: Family
  families: Family[]
  loadingEvents: boolean
  memberMap: Record<string, FamilyMember>
  memberSummaries: MemberSummary[]
  members: FamilyMember[]
  onChangeMode: (mode: CalendarMode) => void
  onChangeFamily: (familyId: string) => void
  onOpenEvent: (event: CalendarEvent) => void
  onOpenNewEvent: (date?: Date) => void
  onOpenTask: (task: Task) => void
  onShiftDate: (direction: -1 | 1) => void
  scheduledTasks: Task[]
  taskColorMap: Record<string, string>
  tasks: Task[]
  timeLabel: string
}) {
  const activeMembers = memberSummaries.length > 0 ? memberSummaries : MEMBER_NAMES.map((name, index) => ({
    name,
    count: 0,
    completed: 0,
    color: PERSON_COLORS[index % PERSON_COLORS.length],
  }))

  const weekDays = useMemo(() => getWeekDays(anchorDate), [anchorDate])
  const monthDays = useMemo(() => getMonthDays(anchorDate), [anchorDate])
  const rangeLabel = calendarMode === 'week' ? formatWeekRange(anchorDate) : formatMonthLabel(anchorDate)

  return (
    <section className="calendar-view" aria-label="Calendar">
      <header className="calendar-header">
        <div>
          <p className="calendar-kicker">{dateLabel}</p>
          <h1>{family?.name ?? 'Family Calendar'}</h1>
          <div className="weather-line">
            <span>{timeLabel}</span>
            <span className="sun-dot" aria-hidden="true" />
            <span>{rangeLabel}</span>
          </div>
        </div>

        <div className="calendar-actions">
          <label className="family-select">
            <span>Family</span>
            <select value={family?.id ?? ''} onChange={(event) => onChangeFamily(event.target.value)} aria-label="Family">
              {families.map((currentFamily) => (
                <option key={currentFamily.id} value={currentFamily.id}>
                  {currentFamily.name}
                </option>
              ))}
            </select>
          </label>
          <div className="segmented-control" aria-label="Calendar view">
            <button className={calendarMode === 'week' ? 'active' : ''} type="button" onClick={() => onChangeMode('week')}>
              Week
            </button>
            <button className={calendarMode === 'month' ? 'active' : ''} type="button" onClick={() => onChangeMode('month')}>
              Month
            </button>
          </div>
          <div className="date-nav" aria-label="Calendar date navigation">
            <button className="soft-button" type="button" onClick={() => onShiftDate(-1)}>
              Back
            </button>
            <button className="soft-button" type="button" onClick={() => onShiftDate(1)}>
              Next
            </button>
          </div>
          <div className="avatar-stack" aria-label="Family members">
            {activeMembers.slice(0, 4).map((member) => (
              <span className="avatar" key={member.name} style={{ '--chip-color': member.color } as CSSProperties}>
                {member.name.charAt(0).toUpperCase()}
              </span>
            ))}
          </div>
        </div>
      </header>

      <section className="member-progress" aria-label="Member progress">
        {activeMembers.slice(0, 4).map((member, index) => {
          const eventCount = events.filter((event) => {
            const eventMember = event.memberId ? memberMap[event.memberId] : undefined
            return eventMember?.name === member.name
          }).length
          const total = Math.max(member.count + eventCount, index + 2)
          const completed = Math.min(total, Math.max(member.completed + eventCount, index + 1))
          const percent = total === 0 ? 0 : Math.round((completed / total) * 100)

          return (
            <article className="progress-card" key={member.name}>
              <span className="avatar small" style={{ '--chip-color': member.color } as CSSProperties}>
                {member.name.charAt(0).toUpperCase()}
              </span>
              <strong>{member.name}</strong>
              <div className="progress-track">
                <span style={{ width: `${percent}%`, background: member.color }} />
              </div>
              <span className="progress-count">
                {completed}/{total}
              </span>
            </article>
          )
        })}
      </section>

      {loadingEvents ? <p className="calendar-loading">Loading events...</p> : null}

      {calendarMode === 'week' ? (
        <WeekCalendar
          events={events}
          memberMap={memberMap}
          onOpenEvent={onOpenEvent}
          onOpenTask={onOpenTask}
          scheduledTasks={scheduledTasks}
          taskColorMap={taskColorMap}
          tasks={tasks}
          weekDays={weekDays}
        />
      ) : (
        <MonthCalendar
          anchorDate={anchorDate}
          events={events}
          memberMap={memberMap}
          monthDays={monthDays}
          onOpenDay={(date) => onOpenNewEvent(date)}
          onOpenEvent={onOpenEvent}
          onOpenTask={onOpenTask}
          scheduledTasks={scheduledTasks}
          taskColorMap={taskColorMap}
        />
      )}

      <button className="floating-add" type="button" aria-label="Add calendar event" onClick={() => onOpenNewEvent()}>
        +
      </button>

      {members.length === 0 ? <p className="calendar-footnote">Create a family member in Settings to assign new events.</p> : null}
    </section>
  )
}

function WeekCalendar({
  events,
  memberMap,
  onOpenEvent,
  onOpenTask,
  scheduledTasks,
  taskColorMap,
  tasks,
  weekDays,
}: {
  events: CalendarEvent[]
  memberMap: Record<string, FamilyMember>
  onOpenEvent: (event: CalendarEvent) => void
  onOpenTask: (task: Task) => void
  scheduledTasks: Task[]
  taskColorMap: Record<string, string>
  tasks: Task[]
  weekDays: Date[]
}) {
  const unscheduledTasks = tasks.filter((task) => !task.dueAt)

  return (
    <section className="calendar-board">
      <div className="calendar-day-header spacer" />
      {weekDays.map((day) => (
        <div className="calendar-day-header" key={day.toISOString()}>
          {formatWeekdayLabel(day)}
        </div>
      ))}

      <div className="time-label all-day-label">All day</div>
      {weekDays.map((day, dayIndex) => (
        <div className="all-day-cell" key={day.toISOString()}>
          {dayIndex === 0
            ? unscheduledTasks.slice(0, 2).map((task) => (
                <button className="all-day-pill task-pill" key={task.id} type="button" onClick={() => onOpenTask(task)}>
                  {task.title}
                </button>
              ))
            : null}
        </div>
      ))}

      <div className="calendar-grid">
        <div className="time-column">
          {TIME_SLOTS.map((hour) => (
            <div className="time-row-label" key={hour}>
              {formatHour(hour)}
            </div>
          ))}
        </div>

        <div className="event-grid">
          {weekDays.map((day) => (
            <div className="day-column" key={day.toISOString()}>
              {TIME_SLOTS.map((hour) => (
                <div className="hour-line" key={hour} />
              ))}
            </div>
          ))}

          {events
            .filter((event) => getWeekdayIndex(new Date(event.startAt), weekDays) >= 0 && getWeekdayIndex(new Date(event.startAt), weekDays) < 7)
            .map((event) => {
              const member = event.memberId ? memberMap[event.memberId] : undefined
              return (
                <button
                  className="calendar-event"
                  key={event.id}
                  style={{
                    ...getEventStyle(event, weekDays),
                    '--event-color': member?.color ?? event.tone ?? '#bfe1df',
                    '--chip-color': member?.color ?? taskColorMap[member?.name ?? ''] ?? '#8fbcc0',
                  } as CSSProperties}
                  type="button"
                  onClick={() => onOpenEvent(event)}
                >
                  <strong>{event.title}</strong>
                  <span>
                    {formatEventTime(event.startAt)} - {formatEventTime(event.endAt)}
                  </span>
                  <em>{member?.name.charAt(0) ?? 'F'}</em>
                </button>
              )
            })}

          {scheduledTasks
            .filter((task) => task.dueAt && getWeekdayIndex(new Date(task.dueAt), weekDays) >= 0)
            .map((task) => (
              <button
                className={`calendar-event chore-event ${task.completed ? 'is-complete' : ''}`}
                key={task.id}
                style={{
                  ...getTaskStyle(task, weekDays),
                  '--event-color': taskColorMap[task.assigneeName || 'Unassigned'] ?? '#cfe6ef',
                  '--chip-color': taskColorMap[task.assigneeName || 'Unassigned'] ?? '#8fbcc0',
                } as CSSProperties}
                type="button"
                onClick={() => onOpenTask(task)}
              >
                <strong>{task.title}</strong>
                <span>{formatTaskTime(task)}</span>
                <em>{task.assigneeName.charAt(0).toUpperCase() || 'C'}</em>
              </button>
            ))}
        </div>
      </div>
    </section>
  )
}

function MonthCalendar({
  anchorDate,
  events,
  memberMap,
  monthDays,
  onOpenDay,
  onOpenEvent,
  onOpenTask,
  scheduledTasks,
  taskColorMap,
}: {
  anchorDate: Date
  events: CalendarEvent[]
  memberMap: Record<string, FamilyMember>
  monthDays: Date[]
  onOpenDay: (date: Date) => void
  onOpenEvent: (event: CalendarEvent) => void
  onOpenTask: (task: Task) => void
  scheduledTasks: Task[]
  taskColorMap: Record<string, string>
}) {
  return (
    <section className="month-board" aria-label="Month calendar">
      {['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'].map((dayName) => (
        <div className="month-weekday" key={dayName}>
          {dayName}
        </div>
      ))}
      {monthDays.map((day) => {
        const dayEvents = events.filter((event) => isSameDay(new Date(event.startAt), day))
        const dayTasks = scheduledTasks.filter((task) => task.dueAt && isSameDay(new Date(task.dueAt), day))
        const dayItems = [
          ...dayEvents.map((event) => ({ type: 'event' as const, event })),
          ...dayTasks.map((task) => ({ type: 'task' as const, task })),
        ]
        const visibleItems = dayItems.slice(0, 3)
        const overflow = dayItems.length - visibleItems.length

        return (
          <article className={`month-day ${day.getMonth() !== anchorDate.getMonth() ? 'muted' : ''}`} key={day.toISOString()}>
            <button className="month-day-number" type="button" onClick={() => onOpenDay(day)} aria-label={`Add event on ${formatDateInput(day)}`}>
              {day.getDate()}
            </button>
            <div className="month-event-list">
              {visibleItems.map((item) => {
                if (item.type === 'task') {
                  return (
                    <button
                      className={`month-event-pill chore-pill ${item.task.completed ? 'is-complete' : ''}`}
                      key={`task-${item.task.id}`}
                      style={{ '--event-color': taskColorMap[item.task.assigneeName || 'Unassigned'] ?? '#cfe6ef' } as CSSProperties}
                      type="button"
                      onClick={() => onOpenTask(item.task)}
                    >
                      {item.task.title}
                    </button>
                  )
                }

                const member = item.event.memberId ? memberMap[item.event.memberId] : undefined
                return (
                  <button
                    className="month-event-pill"
                    key={`event-${item.event.id}`}
                    style={{ '--event-color': member?.color ?? item.event.tone ?? '#bfe1df' } as CSSProperties}
                    type="button"
                    onClick={() => onOpenEvent(item.event)}
                  >
                    {item.event.title}
                  </button>
                )
              })}
              {overflow > 0 ? <span className="month-overflow">+{overflow} more</span> : null}
            </div>
          </article>
        )
      })}
    </section>
  )
}

function EventModal({
  deletingEvent,
  editingEvent,
  eventForm,
  members,
  onClose,
  onDelete,
  onSave,
  savingEvent,
  setEventForm,
}: {
  deletingEvent: boolean
  editingEvent: CalendarEvent | null
  eventForm: EventFormState
  members: FamilyMember[]
  onClose: () => void
  onDelete: () => Promise<void>
  onSave: (event: FormEvent<HTMLFormElement>) => Promise<void>
  savingEvent: boolean
  setEventForm: (updater: (current: EventFormState) => EventFormState) => void
}) {
  return (
    <div className="modal-backdrop" role="presentation">
      <section className="event-modal" role="dialog" aria-modal="true" aria-label={editingEvent ? 'Edit calendar event' : 'New calendar event'}>
        <header className="modal-header">
          <div>
            <span className="board-kicker">{editingEvent ? 'Event details' : 'New event'}</span>
            <h2>{editingEvent ? editingEvent.title : 'Add calendar event'}</h2>
          </div>
          <button className="icon-button" type="button" onClick={onClose} aria-label="Close event modal">
            X
          </button>
        </header>

        <form className="event-form" onSubmit={(event) => void onSave(event)}>
          <label>
            <span>Title</span>
            <input
              value={eventForm.title}
              onChange={(event) => setEventForm((current) => ({ ...current, title: event.target.value }))}
              placeholder="Guitar lesson"
              required
            />
          </label>

          <div className="form-row">
            <label>
              <span>Date</span>
              <input
                type="date"
                value={eventForm.date}
                onChange={(event) => setEventForm((current) => ({ ...current, date: event.target.value }))}
                required
              />
            </label>
            <label>
              <span>Member</span>
              <select value={eventForm.memberId} onChange={(event) => setEventForm((current) => ({ ...current, memberId: event.target.value }))}>
                <option value="">Whole family</option>
                {members.map((member) => (
                  <option key={member.id} value={member.id}>
                    {member.name}
                  </option>
                ))}
              </select>
            </label>
          </div>

          <div className="form-row">
            <label>
              <span>Start</span>
              <input
                type="time"
                value={eventForm.startTime}
                onChange={(event) => setEventForm((current) => ({ ...current, startTime: event.target.value }))}
                required
              />
            </label>
            <label>
              <span>End</span>
              <input
                type="time"
                value={eventForm.endTime}
                onChange={(event) => setEventForm((current) => ({ ...current, endTime: event.target.value }))}
                required
              />
            </label>
          </div>

          <label>
            <span>Notes</span>
            <textarea
              value={eventForm.notes}
              onChange={(event) => setEventForm((current) => ({ ...current, notes: event.target.value }))}
              placeholder="Optional details"
            />
          </label>

          <footer className="modal-actions">
            {editingEvent ? (
              <button className="delete-button" type="button" disabled={deletingEvent || savingEvent} onClick={() => void onDelete()}>
                {deletingEvent ? 'Deleting...' : 'Delete'}
              </button>
            ) : null}
            <button className="ghost-button" type="button" onClick={onClose}>
              Cancel
            </button>
            <button className="primary-button" type="submit" disabled={savingEvent || deletingEvent}>
              {savingEvent ? 'Saving...' : editingEvent ? 'Save changes' : 'Create event'}
            </button>
          </footer>
        </form>
      </section>
    </div>
  )
}

function TaskDetailModal({
  onClose,
  onDelete,
  onToggle,
  pendingMutation,
  task,
  taskColorMap,
}: {
  onClose: () => void
  onDelete: (taskId: string) => Promise<void>
  onToggle: (taskId: string) => Promise<void>
  pendingMutation: boolean
  task: Task
  taskColorMap: Record<string, string>
}) {
  return (
    <div className="modal-backdrop" role="presentation">
      <section className="event-modal" role="dialog" aria-modal="true" aria-label="Chore details">
        <header className="modal-header">
          <div>
            <span className="board-kicker">Chore details</span>
            <h2>{task.title}</h2>
          </div>
          <button className="icon-button" type="button" onClick={onClose} aria-label="Close chore details">
            X
          </button>
        </header>

        <div className="task-detail-body">
          <span
            className="task-badge"
            style={{ '--chip-color': taskColorMap[task.assigneeName || 'Unassigned'] ?? '#94a3b8' } as CSSProperties}
          >
            {task.assigneeName || 'Unassigned'}
          </span>
          <dl className="detail-list">
            <div>
              <dt>Status</dt>
              <dd>{task.completed ? 'Done' : 'Open'}</dd>
            </div>
            <div>
              <dt>Schedule</dt>
              <dd>{task.dueAt ? formatShortDateTime(task.dueAt) : 'Unscheduled'}</dd>
            </div>
            <div>
              <dt>Duration</dt>
              <dd>{task.durationMinutes ? `${task.durationMinutes} minutes` : 'Not set'}</dd>
            </div>
            <div>
              <dt>Repeat</dt>
              <dd>{task.recurrenceRule && task.recurrenceRule !== 'None' ? task.recurrenceRule : 'No repeat'}</dd>
            </div>
          </dl>
        </div>

        <footer className="modal-actions task-detail-actions">
          <button className="delete-button" type="button" disabled={pendingMutation} onClick={() => void onDelete(task.id)}>
            Delete
          </button>
          <button className="ghost-button" type="button" onClick={onClose}>
            Close
          </button>
          <button className={`toggle-button ${task.completed ? 'is-complete' : ''}`} type="button" disabled={pendingMutation} onClick={() => void onToggle(task.id)}>
            {task.completed ? 'Mark open' : 'Mark done'}
          </button>
        </footer>
      </section>
    </div>
  )
}

function ChoresView({
  activeDropKey,
  assigneeName,
  creatingTask,
  dateLabel,
  draggingTaskId,
  family,
  groupedColumns,
  handleDeleteTask,
  handleMoveTask,
  handleSubmit,
  handleToggleTask,
  hideCompleted,
  loadingTasks,
  memberSummaries,
  pendingMutation,
  refetch,
  setActiveDropKey,
  setAssigneeName,
  setDraggingTaskId,
  setHideCompleted,
  setTaskScheduleForm,
  setTitle,
  taskScheduleForm,
  taskColorMap,
  taskSyncError,
  tasks,
  title,
}: {
  activeDropKey: string | null
  assigneeName: string
  creatingTask: boolean
  dateLabel: string
  draggingTaskId: string | null
  family?: Family
  groupedColumns: Array<(typeof STATUS_COLUMNS)[number] & { tasks: Task[] }>
  handleDeleteTask: (taskId: string) => Promise<void>
  handleMoveTask: (targetStatus: TaskStatus, targetOrder: number) => Promise<void>
  handleSubmit: (event: FormEvent<HTMLFormElement>) => Promise<void>
  handleToggleTask: (taskId: string) => Promise<void>
  hideCompleted: boolean
  loadingTasks: boolean
  memberSummaries: MemberSummary[]
  pendingMutation: boolean
  refetch: () => void
  setActiveDropKey: (key: string | null) => void
  setAssigneeName: (name: string) => void
  setDraggingTaskId: (taskId: string | null) => void
  setHideCompleted: (updater: (current: boolean) => boolean) => void
  setTaskScheduleForm: (updater: (current: TaskScheduleFormState) => TaskScheduleFormState) => void
  setTitle: (title: string) => void
  taskScheduleForm: TaskScheduleFormState
  taskColorMap: Record<string, string>
  taskSyncError: string | null
  tasks: Task[]
  title: string
}) {
  return (
    <section className="chores-view" aria-label="Chores">
      <section className="board-topbar">
        <div>
          <span className="board-kicker">Family command center</span>
          <h1 className="board-title">{family?.name ?? 'Family'} task board</h1>
          <p className="board-subtitle">Drag chores through the workflow and keep every family member in motion.</p>

          <div className="board-header-meta">
            <span className="meta-pill">{dateLabel}</span>
            <span className="meta-pill">1 active board</span>
            <span className="meta-pill">{tasks.length} visible tasks</span>
          </div>
        </div>

        <div className="toolbar">
          <div className="toolbar-row">
            <button
              className={`filter-chip ${hideCompleted ? 'active' : ''}`}
              type="button"
              onClick={() => setHideCompleted((current) => !current)}
            >
              {hideCompleted ? 'Active only' : 'All tasks'}
            </button>
            <button className="ghost-button" type="button" onClick={() => refetch()} disabled={pendingMutation}>
              Refresh
            </button>
          </div>

          <section className="quick-add-card">
            <h2>Add a task</h2>
            <form className="quick-add-form" onSubmit={(event) => void handleSubmit(event)}>
              <input
                value={title}
                onChange={(event) => setTitle(event.target.value)}
                placeholder="Unload dishwasher"
                aria-label="Task title"
              />
              <select value={assigneeName} onChange={(event) => setAssigneeName(event.target.value)} aria-label="Assignee">
                {memberSummaries.map((member) => (
                  <option key={member.name} value={member.name}>
                    {member.name}
                  </option>
                ))}
              </select>
              <input
                type="date"
                value={taskScheduleForm.date}
                onChange={(event) => setTaskScheduleForm((current) => ({ ...current, date: event.target.value }))}
                aria-label="Chore date"
              />
              <input
                type="time"
                value={taskScheduleForm.time}
                onChange={(event) => setTaskScheduleForm((current) => ({ ...current, time: event.target.value }))}
                aria-label="Chore time"
              />
              <select
                value={taskScheduleForm.durationMinutes}
                onChange={(event) => setTaskScheduleForm((current) => ({ ...current, durationMinutes: event.target.value }))}
                aria-label="Chore duration"
              >
                <option value="30">30 min</option>
                <option value="60">1 hour</option>
                <option value="90">1.5 hours</option>
                <option value="120">2 hours</option>
              </select>
              <select
                value={taskScheduleForm.recurrenceRule}
                onChange={(event) => setTaskScheduleForm((current) => ({ ...current, recurrenceRule: event.target.value }))}
                aria-label="Chore recurrence"
              >
                <option value="None">No repeat</option>
                <option value="Daily">Daily</option>
                <option value="Weekly">Weekly</option>
                <option value="Monthly">Monthly</option>
              </select>
              <button className="primary-button" type="submit" disabled={creatingTask}>
                {creatingTask ? 'Adding...' : 'Add'}
              </button>
            </form>
          </section>
        </div>
      </section>

      <section className="people-strip" aria-label="Family members summary">
        {memberSummaries.map((member) => (
          <article className="person-card" key={member.name}>
            <div className="person-card-top">
              <span className="person-chip" style={{ '--chip-color': member.color } as CSSProperties}>
                {member.name}
              </span>
              <span>
                {member.completed}/{member.count} done
              </span>
            </div>
            <strong>{member.count}</strong>
            <span>{member.count === 1 ? 'task assigned' : 'tasks assigned'}</span>
          </article>
        ))}
      </section>

      {loadingTasks ? <p className="calendar-loading">Loading chores...</p> : null}

      <section className="board-columns" aria-label="Task board">
        {groupedColumns.map((column) => (
          <article
            className={`column ${activeDropKey === `${column.id}-end` ? 'drag-over' : ''}`}
            key={column.id}
            onDragOver={(event) => {
              event.preventDefault()
              setActiveDropKey(`${column.id}-end`)
            }}
            onDragLeave={() => setActiveDropKey(null)}
            onDrop={(event) => {
              event.preventDefault()
              void handleMoveTask(column.id, column.tasks.length)
            }}
          >
            <header className="column-header">
              <div className="column-title-wrap">
                <h3>{column.title}</h3>
                <p>{column.description}</p>
              </div>
              <span className="column-count">{column.tasks.length}</span>
            </header>

            <div className="column-list">
              {column.tasks.map((task, index) => (
                <div key={task.id}>
                  <div
                    className={`drop-zone ${activeDropKey === `${column.id}-${index}` ? 'active' : ''}`}
                    onDragOver={(event) => {
                      event.preventDefault()
                      event.stopPropagation()
                      setActiveDropKey(`${column.id}-${index}`)
                    }}
                    onDragLeave={() => setActiveDropKey(null)}
                    onDrop={(event) => {
                      event.preventDefault()
                      event.stopPropagation()
                      void handleMoveTask(column.id, index)
                    }}
                  />

                  <article
                    className={`task-card ${draggingTaskId === task.id ? 'is-dragging' : ''}`}
                    draggable={!pendingMutation}
                    onDragStart={() => setDraggingTaskId(task.id)}
                    onDragEnd={() => {
                      setDraggingTaskId(null)
                      setActiveDropKey(null)
                    }}
                  >
                    <div className="task-card-top">
                      <div>
                        <h4>{task.title}</h4>
                        <p>{task.dueAt ? `Scheduled ${formatShortDateTime(task.dueAt)}` : task.completed ? 'Completed task' : 'Open task'}</p>
                      </div>
                      <button className="icon-button" type="button" aria-label="Drag task" title="Drag task">
                        ::
                      </button>
                    </div>

                    <div className="task-card-meta">
                      <span
                        className="task-badge"
                        style={{ '--chip-color': taskColorMap[task.assigneeName || 'Unassigned'] ?? '#94a3b8' } as CSSProperties}
                      >
                        {task.assigneeName || 'Unassigned'}
                      </span>
                      <span className="task-time">
                        Updated {new Date(task.updatedAt).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })}
                      </span>
                    </div>

                    <div className="task-card-actions">
                      <button
                        className={`toggle-button ${task.completed ? 'is-complete' : ''}`}
                        type="button"
                        onClick={() => void handleToggleTask(task.id)}
                        disabled={pendingMutation}
                        title={task.completed ? 'Mark as active' : 'Mark as done'}
                      >
                        {task.completed ? 'Done' : 'Open'}
                      </button>
                      <button
                        className="delete-button"
                        type="button"
                        onClick={() => void handleDeleteTask(task.id)}
                        disabled={pendingMutation}
                        title="Delete task"
                      >
                        Delete
                      </button>
                    </div>
                  </article>
                </div>
              ))}

              {column.tasks.length === 0 ? (
                <div className="empty-column">
                  <div>
                    <h4>{taskSyncError ? 'Tasks unavailable' : 'No tasks here'}</h4>
                    <p>
                      {taskSyncError
                        ? 'Start the GraphQL backend or retry sync to load chores.'
                        : `Drag a card into ${column.title.toLowerCase()} or create a new task above.`}
                    </p>
                  </div>
                </div>
              ) : null}
            </div>
          </article>
        ))}
      </section>
    </section>
  )
}

function RewardsView({ memberSummaries }: { memberSummaries: MemberSummary[] }) {
  return (
    <SectionPage eyebrow="Rewards" title="Family reward progress">
      <div className="section-grid">
        {memberSummaries.map((member, index) => (
          <article className="feature-card" key={member.name}>
            <span className="person-chip" style={{ '--chip-color': member.color } as CSSProperties}>
              {member.name}
            </span>
            <strong>{member.completed * 10 + (index + 1) * 5} points</strong>
            <p>{member.completed >= 3 ? 'Movie night unlocked' : 'Working toward a weekend pick'}</p>
            <div className="progress-track wide">
              <span style={{ width: `${Math.min(100, member.completed * 22 + 24)}%`, background: member.color }} />
            </div>
          </article>
        ))}
      </div>
    </SectionPage>
  )
}

function MealsView() {
  const meals = ['Pasta night', 'Taco bowls', 'Sheet pan chicken', 'Breakfast for dinner', 'Pizza and salad']

  return (
    <SectionPage eyebrow="Meals" title="Weekly meal planner">
      <div className="planner-list">
        {getWeekDays(new Date()).slice(0, 5).map((day, index) => (
          <article className="planner-row" key={day.toISOString()}>
            <span>{formatWeekdayLabel(day)}</span>
            <strong>{meals[index]}</strong>
            <p>{index % 2 === 0 ? 'Groceries ready' : 'Needs prep'}</p>
          </article>
        ))}
      </div>
    </SectionPage>
  )
}

function PhotosView() {
  return (
    <SectionPage eyebrow="Photos" title="Family photo board">
      <div className="photo-grid">
        {['Camping', 'School', 'Dinner', 'Game night', 'Practice', 'Weekend'].map((label, index) => (
          <article className={`photo-tile tile-${index + 1}`} key={label}>
            <span>{label}</span>
          </article>
        ))}
      </div>
    </SectionPage>
  )
}

function ListsView({ tasks }: { tasks: Task[] }) {
  const fallbackItems = ['Pack lunch boxes', 'Restock paper towels', 'Return library books']
  const items = tasks.length > 0 ? tasks.slice(0, 5).map((task) => task.title) : fallbackItems

  return (
    <SectionPage eyebrow="Lists" title="Household checklists">
      <div className="checklist-panel">
        {items.map((item, index) => (
          <label className="check-row" key={item}>
            <input type="checkbox" defaultChecked={index === 0} />
            <span>{item}</span>
          </label>
        ))}
      </div>
    </SectionPage>
  )
}

function SleepView() {
  return (
    <SectionPage eyebrow="Sleep" title="Evening routines">
      <div className="section-grid">
        {['Wind down', 'Reading', 'Lights out'].map((step, index) => (
          <article className="feature-card" key={step}>
            <span className="routine-time">{`${7 + index}:30 PM`}</span>
            <strong>{step}</strong>
            <p>{index === 0 ? 'Bath, pajamas, and tomorrow setup' : 'Quiet routine block'}</p>
          </article>
        ))}
      </div>
    </SectionPage>
  )
}

function SettingsView({
  creatingFamily,
  creatingInvite,
  creatingMember,
  families,
  family,
  handleCreateFamily,
  handleCreateInvite,
  handleCreateMember,
  handleUpdateMember,
  latestInvite,
  memberDrafts,
  members,
  newFamilyName,
  newMemberColor,
  newMemberName,
  onChangeFamily,
  setMemberDrafts,
  setNewFamilyName,
  setNewMemberColor,
  setNewMemberName,
  updatingMember,
}: {
  creatingFamily: boolean
  creatingInvite: boolean
  creatingMember: boolean
  families: Family[]
  family?: Family
  handleCreateFamily: (event: FormEvent<HTMLFormElement>) => Promise<void>
  handleCreateInvite: () => Promise<void>
  handleCreateMember: (event: FormEvent<HTMLFormElement>) => Promise<void>
  handleUpdateMember: (member: FamilyMember) => Promise<void>
  latestInvite: FamilyInvite | null
  memberDrafts: Record<string, { name: string; color: string }>
  members: FamilyMember[]
  newFamilyName: string
  newMemberColor: string
  newMemberName: string
  onChangeFamily: (familyId: string) => void
  setMemberDrafts: (updater: (current: Record<string, { name: string; color: string }>) => Record<string, { name: string; color: string }>) => void
  setNewFamilyName: (name: string) => void
  setNewMemberColor: (color: string) => void
  setNewMemberName: (name: string) => void
  updatingMember: boolean
}) {
  return (
    <SectionPage eyebrow="Settings" title="Family app settings">
      <div className="settings-layout">
        <section className="settings-list">
          <h2>Families</h2>
          <label className="setting-row">
            <span>Active family</span>
            <select value={family?.id ?? ''} onChange={(event) => onChangeFamily(event.target.value)}>
              {families.map((currentFamily) => (
                <option key={currentFamily.id} value={currentFamily.id}>
                  {currentFamily.name}
                </option>
              ))}
            </select>
          </label>

          <form className="setting-row form-setting" onSubmit={(event) => void handleCreateFamily(event)}>
            <span>New family</span>
            <input value={newFamilyName} onChange={(event) => setNewFamilyName(event.target.value)} placeholder="Garcia Family" />
            <button className="primary-button" type="submit" disabled={creatingFamily}>
              {creatingFamily ? 'Creating...' : 'Create family'}
            </button>
          </form>
        </section>

        <section className="settings-list">
          <h2>Invites</h2>
          <div className="setting-row form-setting">
            <span>Invite code</span>
            <strong>{latestInvite?.code ?? 'Create a code to share'}</strong>
            <button className="primary-button" type="button" disabled={creatingInvite || !family} onClick={() => void handleCreateInvite()}>
              {creatingInvite ? 'Creating...' : 'Create invite'}
            </button>
          </div>
          {latestInvite ? <p className="settings-note">Expires {new Date(latestInvite.expiresAt).toLocaleDateString()}.</p> : null}
        </section>

        <section className="settings-list">
          <h2>Members</h2>
          <form className="setting-row form-setting" onSubmit={(event) => void handleCreateMember(event)}>
            <span>New member</span>
            <input value={newMemberName} onChange={(event) => setNewMemberName(event.target.value)} placeholder="Avery" />
            <input
              aria-label="New member color"
              type="color"
              value={newMemberColor}
              onChange={(event) => setNewMemberColor(event.target.value)}
            />
            <button className="primary-button" type="submit" disabled={creatingMember || !family}>
              {creatingMember ? 'Adding...' : 'Add member'}
            </button>
          </form>

          {members.map((member) => {
            const draft = memberDrafts[member.id] ?? { name: member.name, color: member.color }
            return (
              <div className="setting-row form-setting" key={member.id}>
                <span>Member</span>
                <input
                  value={draft.name}
                  onChange={(event) =>
                    setMemberDrafts((current) => ({ ...current, [member.id]: { ...draft, name: event.target.value } }))
                  }
                  aria-label={`${member.name} name`}
                />
                <input
                  aria-label={`${member.name} color`}
                  type="color"
                  value={draft.color}
                  onChange={(event) =>
                    setMemberDrafts((current) => ({ ...current, [member.id]: { ...draft, color: event.target.value } }))
                  }
                />
                <button className="ghost-button" type="button" disabled={updatingMember} onClick={() => void handleUpdateMember(member)}>
                  Save
                </button>
              </div>
            )
          })}
        </section>
      </div>
    </SectionPage>
  )
}

function SectionPage({
  children,
  eyebrow,
  title,
}: {
  children: ReactNode
  eyebrow: string
  title: string
}) {
  return (
    <section className="section-page" aria-label={eyebrow}>
      <span className="board-kicker">{eyebrow}</span>
      <h1 className="board-title">{title}</h1>
      {children}
    </section>
  )
}

function calendarRefetchQueries(familyId: string, visibleRange: { start: Date; end: Date }) {
  if (!familyId) {
    return []
  }

  return [
    {
      query: GET_CALENDAR_EVENTS,
      variables: {
        familyId,
        rangeStart: visibleRange.start.toISOString(),
        rangeEnd: visibleRange.end.toISOString(),
      },
    },
  ]
}

function taskRefetchQueries(queryVariables: { boardId: string; includeCompleted: boolean }, familyId: string, visibleRange: { start: Date; end: Date }) {
  const refetchQueries = [{ query: GET_TASKS, variables: queryVariables }]

  if (!familyId) {
    return refetchQueries
  }

  return [
    ...refetchQueries,
    {
      query: GET_SCHEDULED_TASKS,
      variables: {
        familyId,
        rangeStart: visibleRange.start.toISOString(),
        rangeEnd: visibleRange.end.toISOString(),
      },
    },
  ]
}

function getVisibleRange(anchorDate: Date, mode: CalendarMode) {
  if (mode === 'week') {
    const start = startOfWeek(anchorDate)
    const end = addDays(start, 7)
    return { start, end }
  }

  const start = startOfWeek(new Date(anchorDate.getFullYear(), anchorDate.getMonth(), 1))
  const end = addDays(startOfWeek(new Date(anchorDate.getFullYear(), anchorDate.getMonth() + 1, 1)), 7)
  return { start, end }
}

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

function getWeekDays(anchorDate: Date) {
  const start = startOfWeek(anchorDate)
  return Array.from({ length: 7 }, (_, index) => addDays(start, index))
}

function getMonthDays(anchorDate: Date) {
  const firstOfMonth = new Date(anchorDate.getFullYear(), anchorDate.getMonth(), 1)
  const start = startOfWeek(firstOfMonth)
  return Array.from({ length: 42 }, (_, index) => addDays(start, index))
}

function shiftDate(date: Date, mode: CalendarMode, direction: -1 | 1) {
  if (mode === 'week') {
    return addDays(date, direction * 7)
  }

  return new Date(date.getFullYear(), date.getMonth() + direction, 1)
}

function formatWeekdayLabel(date: Date) {
  return new Intl.DateTimeFormat('en-US', { weekday: 'short', day: 'numeric' }).format(date)
}

function formatWeekRange(date: Date) {
  const start = startOfWeek(date)
  const end = addDays(start, 6)
  return `${new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric' }).format(start)} - ${new Intl.DateTimeFormat('en-US', {
    month: 'short',
    day: 'numeric',
  }).format(end)}`
}

function formatMonthLabel(date: Date) {
  return new Intl.DateTimeFormat('en-US', { month: 'long', year: 'numeric' }).format(date)
}

function formatHour(hour: number) {
  if (hour === 12) {
    return '12 pm'
  }

  return hour > 12 ? `${hour - 12} pm` : `${hour} am`
}

function formatEventTime(value: string) {
  return new Intl.DateTimeFormat('en-US', { hour: 'numeric', minute: '2-digit' }).format(new Date(value))
}

function formatShortDateTime(value: string) {
  return new Intl.DateTimeFormat('en-US', {
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  }).format(new Date(value))
}

function formatTaskTime(task: Task) {
  if (!task.dueAt) {
    return 'Unscheduled'
  }

  const start = new Date(task.dueAt)
  const end = new Date(start)
  end.setMinutes(end.getMinutes() + (task.durationMinutes ?? 60))
  return `${formatEventTime(start.toISOString())} - ${formatEventTime(end.toISOString())}`
}

function getWeekdayIndex(date: Date, weekDays: Date[]) {
  return weekDays.findIndex((day) => isSameDay(date, day))
}

function getEventStyle(event: CalendarEvent, weekDays: Date[]): CSSProperties {
  const eventDate = new Date(event.startAt)
  const endDate = new Date(event.endAt)
  const firstHour = TIME_SLOTS[0]
  const rowHeight = 72
  const startHour = eventDate.getHours() + eventDate.getMinutes() / 60
  const endHour = endDate.getHours() + endDate.getMinutes() / 60
  const top = Math.max(0, (startHour - firstHour) * rowHeight)
  const height = Math.max(54, (endHour - startHour) * rowHeight - 8)
  const dayIndex = getWeekdayIndex(eventDate, weekDays)

  return {
    gridColumn: `${dayIndex + 1}`,
    height,
    top,
  }
}

function getTaskStyle(task: Task, weekDays: Date[]): CSSProperties {
  const startAt = task.dueAt ? new Date(task.dueAt) : new Date()
  const endAt = new Date(startAt)
  endAt.setMinutes(endAt.getMinutes() + (task.durationMinutes ?? 60))

  return getEventStyle(
    {
      id: task.id,
      familyId: task.familyId ?? '',
      title: task.title,
      startAt: startAt.toISOString(),
      endAt: endAt.toISOString(),
      notes: '',
      tone: '#cfe6ef',
    },
    weekDays,
  )
}

function isSameDay(left: Date, right: Date) {
  return left.getFullYear() === right.getFullYear() && left.getMonth() === right.getMonth() && left.getDate() === right.getDate()
}

function formatDateInput(date: Date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}

function createDefaultEventForm(date: Date, memberId = ''): EventFormState {
  return {
    title: '',
    date: formatDateInput(date),
    startTime: '09:00',
    endTime: '10:00',
    memberId,
    notes: '',
  }
}

function createDefaultTaskScheduleForm(date: Date): TaskScheduleFormState {
  void date

  return {
    date: '',
    time: '09:00',
    durationMinutes: '60',
    recurrenceRule: 'None',
  }
}

function createEventFormFromEvent(event: CalendarEvent): EventFormState {
  const startAt = new Date(event.startAt)
  const endAt = new Date(event.endAt)
  return {
    title: event.title,
    date: formatDateInput(startAt),
    startTime: formatTimeInput(startAt),
    endTime: formatTimeInput(endAt),
    memberId: event.memberId ?? '',
    notes: event.notes ?? '',
  }
}

function formatTimeInput(date: Date) {
  return `${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`
}

function buildDateTime(date: string, time: string) {
  return new Date(`${date}T${time}:00`)
}

export default App
