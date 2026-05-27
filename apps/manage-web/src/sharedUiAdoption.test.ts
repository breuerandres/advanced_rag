import { describe, expect, test } from 'vitest';
import appSource from './App.tsx?raw';
import managementNavSource from './components/ManagementNav.tsx?raw';
import accountSource from './features/account/AccountPage.tsx?raw';
import auditSource from './features/audit/AuditPage.tsx?raw';
import configurationSource from './features/configuration/ConfigurationPage.tsx?raw';
import documentsSource from './features/documents/DocumentsPage.tsx?raw';
import richTextEditorSource from './features/documents/RichTextEditor.tsx?raw';
import reportingSource from './features/reporting/FeedbackReviewPage.tsx?raw';
import usersSource from './features/users/UsersBudgetPage.tsx?raw';

describe('shared UI adoption', () => {
  test('uses the shared Button primitive instead of the local button copy', () => {
    const localButtonImports = [
      appSource,
      managementNavSource,
      accountSource,
      auditSource,
      configurationSource,
      documentsSource,
      richTextEditorSource,
      reportingSource,
      usersSource,
    ].filter((content) => content.includes('/components/ui/button'));

    expect(localButtonImports).toEqual([]);
  });
});
