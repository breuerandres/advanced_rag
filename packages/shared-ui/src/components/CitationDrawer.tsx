import { CitationCard } from './CitationCard';
import { Drawer } from './Drawer';

export interface CitationDrawerItem {
  id: string;
  title: string;
  headingPath?: string[];
  href?: string;
  onOpen?: () => void;
}

export interface CitationDrawerProps {
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
  citations: CitationDrawerItem[];
}

export function CitationDrawer({ citations, ...props }: CitationDrawerProps) {
  return (
    <Drawer title="Citas" {...props}>
      <div className="grid gap-2">
        {citations.map((citation) => (
          citation.onOpen ? (
            <button
              key={citation.id}
              type="button"
              className="w-full text-left"
              aria-label={`Abrir cita ${citation.title}`}
              onClick={citation.onOpen}
            >
              <CitationCard
                title={citation.title}
                {...(citation.headingPath ? { headingPath: citation.headingPath } : {})}
              />
            </button>
          ) : (
            <CitationCard
              key={citation.id}
              title={citation.title}
              {...(citation.headingPath ? { headingPath: citation.headingPath } : {})}
              {...(citation.href ? { href: citation.href } : {})}
            />
          )
        ))}
      </div>
    </Drawer>
  );
}
