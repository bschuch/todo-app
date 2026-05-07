import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import { ApolloClient, ApolloLink, InMemoryCache, HttpLink } from '@apollo/client'
import { ApolloProvider } from '@apollo/client/react'

const graphQlUrl = import.meta.env.VITE_GRAPHQL_URL ?? `${window.location.protocol}//${window.location.hostname}:5288/graphql`
const authLink = new ApolloLink((operation, forward) => {
  const token = localStorage.getItem('todo-app-session-token')
  operation.setContext(({ headers = {} }) => ({
    headers: {
      ...headers,
      ...(token ? { authorization: `Bearer ${token}` } : {}),
    },
  }))

  return forward(operation)
})

const client = new ApolloClient({
  link: authLink.concat(new HttpLink({
    uri: graphQlUrl,
  })),
  cache: new InMemoryCache(),
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ApolloProvider client={client}>
      <App />
    </ApolloProvider>
  </StrictMode>,
)
