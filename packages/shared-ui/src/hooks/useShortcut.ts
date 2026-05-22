import { useEffect } from 'react';

type Mod = 'Meta' | 'Ctrl' | 'Alt' | 'Shift';

interface ShortcutDef {
  /** The key the user must press, lowercase. E.g. 'k', 'Escape', '/'. */
  key: string;
  /**
   * Modifiers required. 'Meta' = Cmd on macOS, Ctrl on others. 'Ctrl' explicitly Ctrl.
   * Default 'Meta' so Cmd+K on Mac == Ctrl+K elsewhere via the shorthand.
   */
  mods?: Mod[];
  /** If true, the shortcut still fires when the focus is inside an input/textarea. */
  allowInInput?: boolean;
}

const isMac =
  typeof navigator !== 'undefined' && /Mac|iPhone|iPad/.test(navigator.platform);

function matches(e: KeyboardEvent, def: ShortcutDef): boolean {
  if (e.key.toLowerCase() !== def.key.toLowerCase()) return false;

  const required = def.mods ?? [];
  const wantMeta = required.includes('Meta');
  const wantCtrl = required.includes('Ctrl');
  const wantAlt = required.includes('Alt');
  const wantShift = required.includes('Shift');

  // On non-Mac, Meta is mapped to Ctrl.
  const metaOrCtrl = isMac ? e.metaKey : e.ctrlKey;

  if (wantMeta && !metaOrCtrl) return false;
  if (wantCtrl && !e.ctrlKey) return false;
  if (wantAlt !== e.altKey) return false;
  if (wantShift !== e.shiftKey) return false;
  if (!wantMeta && !wantCtrl && metaOrCtrl) return false;
  return true;
}

function isEditable(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  const tag = target.tagName;
  if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') return true;
  return target.isContentEditable;
}

/**
 * Bind a keyboard shortcut to a handler.
 *
 * Usage:
 *   useShortcut({ key: 'k', mods: ['Meta'] }, () => setCmdkOpen(true));
 *   useShortcut({ key: 'Escape' }, () => closeDrawer());
 */
export function useShortcut(def: ShortcutDef, handler: (e: KeyboardEvent) => void): void {
  useEffect(() => {
    const listener = (e: KeyboardEvent) => {
      if (!def.allowInInput && isEditable(e.target)) return;
      if (!matches(e, def)) return;
      e.preventDefault();
      handler(e);
    };
    window.addEventListener('keydown', listener);
    return () => window.removeEventListener('keydown', listener);
  }, [def, handler]);
}
