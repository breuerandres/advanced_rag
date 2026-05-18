import { ClipboardList, Search } from 'lucide-react'

export function AuditPage() {
  return (
    <section className="workspace" id="audit">
      <header className="workspace-header">
        <div>
          <p className="eyebrow">Control</p>
          <h1>Auditoria</h1>
        </div>
      </header>

      <section className="filter-bar" aria-label="Filtros de auditoria">
        <label className="field filter-search">
          <span>Buscar eventos</span>
          <span className="search-control">
            <Search size={16} />
            <input type="search" placeholder="Documento, usuario o request ID" disabled />
          </span>
        </label>
        <label className="field filter-status">
          <span>Tipo</span>
          <select disabled>
            <option>Todos</option>
          </select>
        </label>
      </section>

      <div className="empty-panel">
        <ClipboardList size={22} />
        <div>
          <h2>Auditoria funcional</h2>
          <p>No hay eventos para los filtros seleccionados.</p>
        </div>
      </div>
    </section>
  )
}
