import { useCallback, useEffect, useState } from 'react';

export type Theme = 'light' | 'dark' | 'system';

const STORAGE_KEY = 'helpcenter:theme';

function getSystemTheme(): 'light' | 'dark' {
  if (typeof window === 'undefined') return 'light';
  if (typeof window.matchMedia !== 'function') return 'light';
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

function getStoredTheme(): Theme {
  if (typeof window === 'undefined') return 'system';
  try {
    const storage = window.localStorage;
    if (!storage || typeof storage.getItem !== 'function') {
      return 'system';
    }
    const stored = storage.getItem(STORAGE_KEY);
    return stored === 'light' || stored === 'dark' || stored === 'system' ? stored : 'system';
  } catch {
    return 'system';
  }
}

function saveStoredTheme(theme: Theme) {
  if (typeof window === 'undefined') return;
  try {
    const storage = window.localStorage;
    if (storage && typeof storage.setItem === 'function') {
      storage.setItem(STORAGE_KEY, theme);
    }
  } catch {
    // localStorage may be disabled / quota-exceeded; non-fatal.
  }
}

function applyTheme(theme: Theme): 'light' | 'dark' {
  const resolved = theme === 'system' ? getSystemTheme() : theme;
  if (typeof document !== 'undefined') {
    document.documentElement.dataset.theme = resolved;
  }
  return resolved;
}

/**
 * Theme state with light/dark/system support, persistence, and OS-pref reactivity.
 *
 * Usage:
 *   const { theme, resolved, setTheme, toggle } = useTheme();
 *   <button onClick={toggle}>{resolved === 'dark' ? '☀' : '🌙'}</button>
 */
export function useTheme(): {
  theme: Theme;
  resolved: 'light' | 'dark';
  setTheme: (theme: Theme) => void;
  toggle: () => void;
} {
  const [theme, setThemeState] = useState<Theme>(() => getStoredTheme());
  const [resolved, setResolved] = useState<'light' | 'dark'>(() => applyTheme(theme));

  const setTheme = useCallback((next: Theme) => {
    setThemeState(next);
    saveStoredTheme(next);
    setResolved(applyTheme(next));
  }, []);

  const toggle = useCallback(() => {
    setTheme(resolved === 'dark' ? 'light' : 'dark');
  }, [resolved, setTheme]);

  // Reactive to OS preference changes when in 'system' mode.
  useEffect(() => {
    if (theme !== 'system') return;
    if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') return;
    const mq = window.matchMedia('(prefers-color-scheme: dark)');
    const handler = () => setResolved(applyTheme('system'));
    mq.addEventListener('change', handler);
    return () => mq.removeEventListener('change', handler);
  }, [theme]);

  return { theme, resolved, setTheme, toggle };
}
