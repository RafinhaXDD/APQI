export default function App() {
  return (
    <div className="flex min-h-dvh flex-col">
      <header className="bg-primary-dark text-surface">
        <div className="mx-auto flex max-w-5xl items-center px-4 py-3">
          <a href="/" className="text-lg font-semibold">
            Book Exchange
          </a>
        </div>
      </header>

      <main className="mx-auto w-full max-w-5xl flex-1 px-4 py-8">
        <section className="border-border bg-surface rounded-lg border p-6">
          <h1 className="text-2xl font-semibold">Find books near you</h1>
          <p className="text-text-muted mt-2">
            List the books you've finished, discover what neighbours are offering, and swap in
            person.
          </p>
        </section>
      </main>

      <footer className="bg-primary-dark text-surface">
        <div className="mx-auto max-w-5xl px-4 py-3 text-sm">Local first. No shipping.</div>
      </footer>
    </div>
  )
}
