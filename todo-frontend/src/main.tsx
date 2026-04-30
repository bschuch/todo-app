import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import { ApolloClient, InMemoryCache, HttpLink } from '@apollo/client'
import { ApolloProvider } from '@apollo/client/react'

const graphQlUrl = import.meta.env.VITE_GRAPHQL_URL ?? `${window.location.protocol}//${window.location.hostname}:5288/graphql`

const client = new ApolloClient({
  link: new HttpLink({
    uri: graphQlUrl,
  }),
  cache: new InMemoryCache(),
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ApolloProvider client={client}>
      <App />
    </ApolloProvider>
  </StrictMode>,
)
