import { gql } from '@apollo/client'
import { useQuery, useMutation } from '@apollo/client/react'
import './App.css'
import { useState } from 'react';

interface Todo {
  id: number;
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




function App() {
  // const [title, setTitle] = React.useState('');
    const [title, setTitle] = useState('');
  const { loading, error, data } = useQuery<GetTodosData>(GET_TODOS);

  // Setup the mutation hook, refetch the todos after adding a new one
  const [addToDo] = useMutation(ADD_TODO, {
    refetchQueries: [{ query: GET_TODOS }],
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (title.trim() === '') return;

    addToDo({ variables: { title } });
    setTitle('');
  };





  if (loading) return <p>Loading...</p>;
  if (error) return <p>Error: {error.message}</p>;
  if (!data) return <p>No data</p>;

  return (
    <div className="App">
      <h1>Todo List</h1>
      <form onSubmit={handleSubmit}>
        <input
          type="text"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          placeholder="Add a new todo"
        />
        <button type="submit">Add</button>
      </form>
      <ul>
        {data.todos.map((todo: Todo) => (
          <li key={todo.id}>
            <span style={{ textDecoration: todo.completed ? 'line-through' : 'none' }}>
              {todo.title}
            </span>
          </li>
        ))}
      </ul>
    </div>
  )
}

export default App
