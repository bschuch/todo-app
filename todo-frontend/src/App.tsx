import { gql } from '@apollo/client'
import { useQuery, useMutation } from '@apollo/client/react'
import './App.css'
import { useState } from 'react';

interface Todo {
  id: string;
  title: string;
  completed: boolean;
}

interface GetTodosData {
  todos: Todo[];
}

const GET_TODOS = gql`
  query GetTodos {
    todos {
      id
      title
      completed
    }
  }
`;

const ADD_TODO = gql`
  mutation AddToDo($title: String!) {
    addTodo(title: $title) {
      id
      title
      completed
    }
  }
`;

const TOGGLE_TODO = gql`
  mutation ToggleToDo($id: String!) {
    toggleTodoCompletion(id: $id) {
      id
      completed
    }
  }
`;

const DELETE_TODO = gql`
  mutation DeleteTodo($id: String!) {
    deleteTodo(id: $id)
  }
`;




function App() {
  // const [title, setTitle] = React.useState('');
    const [title, setTitle] = useState('');
  const { loading, error, data } = useQuery<GetTodosData>(GET_TODOS);

  // Setup the mutation hook, refetch the todos after adding a new one
  const [addToDo] = useMutation(ADD_TODO, {
    refetchQueries: [{ query: GET_TODOS }],
  });

  // 2 Setup the toggle mutation hook, also refetch the todos after toggling
  const [toggleToDo] = useMutation(TOGGLE_TODO, {
    refetchQueries: [{ query: GET_TODOS }],
  });

  const [deleteTodo] = useMutation(DELETE_TODO, {
    refetchQueries: [{ query: GET_TODOS }],
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (title.trim() === '') return;

    addToDo({ variables: { title } });
    setTitle('');
  };

  // 3. Add a click handler for toggling todos
  const handleToggle = (id: string) => {
    toggleToDo({ variables: { id } });
  };

  const handleDelete = (id: string) => {
    deleteTodo({ variables: { id } });
  };





  if (loading) return <p>Loading...</p>;
  if (error) return <p>Error: {error.message}</p>;
  if (!data) return <p>No data</p>;

  return (
    <div style={{ padding: '2rem', fontFamily: 'sans-serif' }}>
      <h1>Todo List</h1>
      <form onSubmit={handleSubmit} style={{ marginBottom: '1rem' }}>
        <input
          value={title} 
          onChange={(e) => setTitle(e.target.value)} 
          placeholder="Add a new to-do..."
          style={{ padding: '0.5rem', marginRight: '0.5rem' }}
        />
        <button type="submit">Add</button>
      </form>
      <ul style={{ listStyle: 'none', padding: 0 }}>
        {data.todos.map((todo: Todo) => (
          // 4. Add an onClick handler to toggle the todo when clicked and add pointer cursor style
            <li key={todo.id} onClick={() => handleToggle(todo.id)} style={{ 
              textDecoration: todo.completed ? 'line-through' : 'none',
              marginBottom: '0.5rem',
              cursor: 'pointer',
              padding: '0.5rem',
              backgroundColor: '#f9f9f9',
              color: '#111827',
              borderRadius: '4px',
              width: 'fit-content'
            }}>
              <span>{todo.title}</span>
              <button
                type="button"
                onClick={(e) => {
                  e.stopPropagation();
                  handleDelete(todo.id);
                }}
                style={{ marginLeft: '0.5rem' }}
              >
                Delete
              </button>
          </li>
        ))}
      </ul>
    </div>
  )
}

export default App
