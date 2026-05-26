import { Toaster, toast } from 'sonner';

export const notify = toast;

export function ToastViewport() {
  return (
    <Toaster
      richColors
      closeButton
      toastOptions={{
        style: {
          background: 'var(--bg-elevated)',
          color: 'var(--fg)',
          borderColor: 'var(--border)',
        },
      }}
    />
  );
}
