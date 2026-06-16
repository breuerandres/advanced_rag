import { afterEach, describe, expect, test, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import App from "./App";
import i18n from "./i18n";

const organizationalUnitsResponse = [
  {
    id: "01000000-0000-0000-0000-000000000001",
    name: "Empresa",
    parentId: null,
    depth: 0,
    isActive: true,
  },
  {
    id: "0c000000-0000-0000-0000-0000000000c0",
    name: "Comunicación",
    parentId: "01000000-0000-0000-0000-000000000001",
    depth: 1,
    isActive: true,
  },
];

const documentTypesResponse = [
  { id: "d0000000-0000-0000-0000-000000000001", name: "Politica", isActive: true, sortOrder: 0 },
  { id: "d0000000-0000-0000-0000-000000000002", name: "Procedimiento", isActive: true, sortOrder: 1 },
  { id: "d0000000-0000-0000-0000-000000000003", name: "Manual", isActive: true, sortOrder: 2 },
];

const usersResponse = [
  {
    id: "11111111-1111-1111-1111-111111111111",
    email: "ana@example.com",
    displayName: "Ana Gomez",
    isActive: true,
    roles: ["Admin"],
    groups: [
      { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
    ],
    organizationalUnit: {
      id: "0a000000-0000-0000-0000-0000000000a3",
      name: "Sistemas",
      parentId: "01000000-0000-0000-0000-000000000001",
      depth: 1,
      isActive: true,
    },
    accessScopeHash: "scope-hash",
    monthlyBudgetUsd: 5,
    currentSpendUsd: 1.25,
    remainingBudgetUsd: 3.75,
    isBudgetDisabled: false,
  },
];

const sessionUser = {
  id: "11111111-1111-1111-1111-111111111111",
  email: "ana@example.com",
  displayName: "Ana Gomez",
  roles: ["Admin"],
  groups: [{ id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" }],
};

const documentPublisherSessionUser = {
  ...sessionUser,
  roles: ["DocumentPublisher"],
};

const documentEditorSessionUser = {
  ...sessionUser,
  roles: ["DocumentEditor"],
};

const viewerSessionUser = {
  ...sessionUser,
  roles: ["Viewer"],
};

const documentsResponse = [
  {
    id: "55555555-5555-5555-5555-555555555555",
    title: "Politica de seguridad",
    state: "Draft",
    documentType: "Politica",
    allowedGroupIds: ["22222222-2222-2222-2222-222222222222"],
    draftVersionNumber: 1,
    publishedVersionNumber: null,
    indexingStatus: "None",
    updatedAt: "2026-05-17T12:00:00Z",
  },
  {
    id: "66666666-6666-6666-6666-666666666666",
    title: "Procedimiento de compras",
    state: "In Review",
    documentType: "Procedimiento",
    allowedGroupIds: ["44444444-4444-4444-4444-444444444444"],
    draftVersionNumber: 2,
    publishedVersionNumber: 1,
    indexingStatus: "Pending",
    updatedAt: "2026-05-17T13:00:00Z",
  },
];

const documentDetail = {
  id: "55555555-5555-5555-5555-555555555555",
  title: "Politica de seguridad",
  state: "Draft",
  currentDraftVersion: {
    id: "77777777-7777-7777-7777-777777777777",
    versionNumber: 1,
    state: "Draft",
    title: "Politica de seguridad",
    documentTypeId: "d0000000-0000-0000-0000-000000000001",
    documentType: "Politica",
    contentHtml: "<p>Usar credencial visible.</p>",
    indexingStatus: "None",
  },
  currentPublishedVersion: null,
  allowedGroupIds: ["22222222-2222-2222-2222-222222222222"],
  updatedAt: "2026-05-17T12:00:00Z",
};

const inReviewDocumentDetail = {
  ...documentDetail,
  id: "66666666-6666-6666-6666-666666666666",
  title: "Procedimiento de compras",
  state: "In Review",
  currentDraftVersion: {
    ...documentDetail.currentDraftVersion!,
    id: "99999999-9999-9999-9999-999999999999",
    versionNumber: 2,
    state: "In Review",
    title: "Procedimiento de compras",
    documentType: "Procedimiento",
    indexingStatus: "Pending",
  },
  currentPublishedVersion: {
    ...documentDetail.currentDraftVersion!,
    id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    versionNumber: 1,
    state: "Published",
    title: "Procedimiento de compras",
    documentType: "Procedimiento",
    indexingStatus: "Succeeded",
  },
  allowedGroupIds: ["44444444-4444-4444-4444-444444444444"],
  updatedAt: "2026-05-17T13:00:00Z",
};

const publishedDocumentDetail = {
  ...documentDetail,
  id: "aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb",
  title: "Manual publicado",
  state: "Published",
  currentDraftVersion: null,
  currentPublishedVersion: {
    ...documentDetail.currentDraftVersion!,
    id: "bbbbbbbb-1111-2222-3333-cccccccccccc",
    versionNumber: 1,
    state: "Published",
    title: "Manual publicado",
    documentTypeId: "d0000000-0000-0000-0000-000000000003",
    documentType: "Manual",
    contentHtml: "<p>Contenido vigente publicado.</p>",
    indexingStatus: "Succeeded",
  },
  updatedAt: "2026-05-17T14:00:00Z",
};

const configurationResponse = {
  customerTimezone: "America/Argentina/Buenos_Aires",
  llmProvider: "openai",
  chatModel: "gpt-4.1-nano",
  embeddingModel: "text-embedding-3-small",
  embeddingDimensions: 1024,
  defaultMonthlyAiBudgetUsd: 5,
  semanticCacheTtlHours: 24,
  semanticCacheSimilarityThreshold: 0.9,
  chatMaxQuestionChars: 4000,
  importMaxFileSizeMb: 10,
  secrets: [
    { name: "OpenAI API key", status: "Configured" },
    { name: "JWT signing keys", status: "Configured" },
  ],
};

const auditEventsResponse = [
  {
    id: "99999999-9999-9999-9999-999999999999",
    actorUserId: "11111111-1111-1111-1111-111111111111",
    actorDisplayName: "Ana Gomez",
    eventType: "document.created",
    eventLabel: "Documento creado",
    entityType: "document",
    entityId: "55555555-5555-5555-5555-555555555555",
    details: { documentId: "55555555-5555-5555-5555-555555555555" },
    requestId: "req-doc-create",
    createdAt: "2026-05-20T12:00:00Z",
  },
];

afterEach(() => {
  void i18n.changeLanguage("es-AR");
  vi.unstubAllGlobals();
});

describe("management users and budgets", () => {
  test("changes the authenticated shell language and hides Portuguese", async () => {
    stubFetch(
      [
        jsonResponse(200, usersResponse),
        jsonResponse(200, [
          { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
        ]),
      ],
      { session: sessionUser },
    );
    const user = userEvent.setup();

    render(<App />);

    await user.selectOptions(await screen.findByLabelText("Idioma"), "en-US");

    expect(screen.queryByRole("option", { name: "PT" })).not.toBeInTheDocument();
    expect(screen.getByText("Active session")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Sign out/i })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Users & groups" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Create user" })).toBeInTheDocument();
    expect(screen.getByText("Active users")).toBeInTheDocument();
  });

  test("shows first-run setup and creates the first administrator", async () => {
    const fetchMock = stubFetch(
      [
        csrfResponse(),
        jsonResponse(201, {
          user: {
            id: "99999999-9999-9999-9999-999999999999",
            email: "admin@example.com",
            displayName: "Admin Inicial",
            roles: ["Admin"],
          },
        }),
      ],
      { setupRequired: true },
    );
    const user = userEvent.setup();

    render(<App />);

    expect(
      await screen.findByRole("heading", {
        name: "Configurá el primer administrador",
      }),
    ).toBeInTheDocument();

    await user.type(
      screen.getByRole("textbox", { name: "Email" }),
      "admin@example.com",
    );
    await user.type(
      screen.getByRole("textbox", { name: "Nombre visible" }),
      "Admin Inicial",
    );
    await user.type(
      screen.getByLabelText("Contraseña"),
      "Correct Horse Battery Staple 42!",
    );
    await user.click(
      screen.getByRole("button", { name: "Crear administrador" }),
    );

    expect(
      await screen.findByText(
        "Administrador creado. Iniciá sesión para continuar.",
      ),
    ).toBeInTheDocument();
    expect(fetchMock).toHaveBeenLastCalledWith(
      "/api/setup/admin",
      expect.objectContaining({ method: "POST" }),
    );
  });

  test("shows login when setup is complete and opens the shell after authentication", async () => {
    stubFetch(
      [
        csrfResponse(),
        jsonResponse(200, { user: sessionUser }),
        jsonResponse(200, usersResponse),
        jsonResponse(200, [
          { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
        ]),
      ],
      { session: null },
    );
    const user = userEvent.setup();

    render(<App />);

    expect(
      await screen.findByRole("heading", { name: "Ingresá a la consola" }),
    ).toBeInTheDocument();

    await user.type(
      screen.getByRole("textbox", { name: "Email" }),
      "ana@example.com",
    );
    await user.type(screen.getByLabelText("Contraseña"), "password");
    await user.click(screen.getByRole("button", { name: "Ingresar" }));

    expect(
      await screen.findByRole("heading", { name: "Usuarios y grupos" }),
    ).toBeInTheDocument();
    expect(screen.getByText("Ana Gomez")).toBeInTheDocument();
  });

  test("shows language and dark mode controls on the login surface", async () => {
    stubFetch([], { session: null });
    const user = userEvent.setup();

    render(<App />);

    expect(
      await screen.findByRole("heading", { name: /Ingres.*a la consola/i }),
    ).toBeInTheDocument();

    await user.selectOptions(screen.getByLabelText("Idioma"), "en-US");

    expect(screen.queryByRole("option", { name: "PT" })).not.toBeInTheDocument();
    expect(
      screen.getByRole("heading", { name: "Sign in to the console" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Change theme" }),
    ).toBeInTheDocument();
  });

  test("does not show language and dark mode controls on first-run setup", async () => {
    stubFetch([], { setupRequired: true });

    render(<App />);

    expect(
      await screen.findByRole("heading", {
        name: /Configur.*primer administrador/i,
      }),
    ).toBeInTheDocument();
    expect(screen.queryByLabelText("Idioma")).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Cambiar tema" }),
    ).not.toBeInTheDocument();
  });

  test("logs out and returns to the login surface", async () => {
    stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      csrfResponse(),
      jsonResponse(200, { status: "ok" }),
    ]);
    const user = userEvent.setup();

    render(<App />);

    expect(
      await screen.findByRole("heading", { name: "Usuarios y grupos" }),
    ).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Cerrar sesión" }));

    expect(
      await screen.findByRole("heading", { name: "Ingresá a la consola" }),
    ).toBeInTheDocument();
  });

  test("shows persistent management navigation sections", async () => {
    stubFetch([jsonResponse(200, usersResponse), jsonResponse(200, [])]);

    render(<App />);

    expect(
      await screen.findByRole("heading", { name: "Usuarios y grupos" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: "Documentos" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: "Usuarios y grupos" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Auditoría" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Feedback" })).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: "Configuración" }),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("link", { name: "Presupuestos IA" }),
    ).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Mi cuenta" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Chat" })).toHaveAttribute(
      "href",
      "https://chat.localhost/",
    );
    expect(screen.getByRole("link", { name: "Chat" })).toHaveAttribute(
      "target",
      "_blank",
    );
    expect(screen.getByRole("link", { name: "Chat" })).toHaveAttribute(
      "rel",
      "noopener noreferrer",
    );
    expect(screen.getByRole("link", { name: "Docs" })).toHaveAttribute(
      "href",
      "https://docs.localhost/",
    );
    expect(screen.getByRole("link", { name: "Docs" })).toHaveAttribute(
      "target",
      "_blank",
    );
    expect(screen.queryByText("Consola de gestión")).not.toBeInTheDocument();
  });

  test("opens product surfaces in a new tab with a one-time session handoff", async () => {
    const open = vi.fn();
    vi.stubGlobal("open", open);
    const fetchMock = stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      csrfResponse(),
      jsonResponse(200, {
        target: "chat",
        handoffCode: "handoff-code",
        expiresAt: "2026-06-05T13:00:00Z",
      }),
    ]);
    const user = userEvent.setup();

    render(<App />);

    expect(
      await screen.findByRole("heading", { name: "Usuarios y grupos" }),
    ).toBeInTheDocument();

    await user.click(screen.getByRole("link", { name: "Chat" }));

    expect(fetchMock).toHaveBeenCalledWith(
      "/api/auth/session-handoffs",
      expect.objectContaining({ method: "POST" }),
    );
    expect(open).toHaveBeenCalledWith(
      "https://chat.localhost/?handoff=handoff-code",
      "_blank",
      "noopener,noreferrer",
    );
  });

  test("renders only self-service navigation for viewers", async () => {
    stubFetch([], { session: viewerSessionUser });

    render(<App />);

    expect(
      await screen.findByRole("heading", { name: "Mi cuenta" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Mi cuenta" })).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: "Configuración" }),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("link", { name: "Documentos" }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("link", { name: "Usuarios y grupos" }),
    ).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Auditoría" })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Feedback" })).not.toBeInTheDocument();
  });

  test("shows document-publisher user read models and hides admin-only user actions", async () => {
    stubFetch(
      [
        jsonResponse(200, usersResponse),
        jsonResponse(200, [
          { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
        ]),
      ],
      { session: documentPublisherSessionUser },
    );
    const user = userEvent.setup();

    render(<App />);

    expect(
      await screen.findByRole("heading", { name: "Usuarios y grupos" }),
    ).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Crear usuario" })).not.toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Editar usuario Ana Gomez" }),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Editar presupuesto de Ana Gomez" }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Dar de baja a Ana Gomez" }),
    ).not.toBeInTheDocument();

    await user.click(screen.getByRole("tab", { name: "Grupos" }));

    expect(screen.getByRole("button", { name: "Crear grupo" })).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Editar grupo Operaciones" }),
    ).toBeInTheDocument();
  });

  test("updates the current user's email and password from the account screen", async () => {
    const updatedSession = {
      ...sessionUser,
      email: "nueva@example.com",
    };
    const fetchMock = stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      csrfResponse(),
      jsonResponse(200, { user: updatedSession }),
      csrfResponse(),
      jsonResponse(200, { status: "ok" }),
    ]);
    const user = userEvent.setup();

    render(<App />);

    expect(
      await screen.findByRole("heading", { name: "Usuarios y grupos" }),
    ).toBeInTheDocument();

    await user.click(screen.getByRole("link", { name: "Mi cuenta" }));
    expect(
      await screen.findByRole("heading", { name: "Mi cuenta" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Guardar email" })).toHaveClass(
      "account-save-button",
    );
    expect(
      screen.getByRole("button", { name: /Cambiar contrase/i }),
    ).toHaveClass("account-save-button");

    await user.clear(screen.getByRole("textbox", { name: "Email" }));
    await user.type(screen.getByRole("textbox", { name: "Email" }), "nueva@example.com");
    await user.click(screen.getByRole("button", { name: "Guardar email" }));

    await user.type(screen.getByLabelText("Contraseña actual"), "password");
    await user.type(screen.getByLabelText("Nueva contraseña"), "new-password");
    await user.click(screen.getByRole("button", { name: "Cambiar contraseña" }));

    expect(fetchMock).toHaveBeenCalledWith(
      "/api/account/email",
      expect.objectContaining({ method: "PUT" }),
    );
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/account/password",
      expect.objectContaining({ method: "PUT" }),
    );
    expect(await screen.findByText("Contraseña actualizada.")).toBeInTheDocument();
  });

  test("keeps active session controls at the bottom of the sidebar", async () => {
    stubFetch([jsonResponse(200, usersResponse), jsonResponse(200, [])]);

    render(<App />);

    expect(
      await screen.findByRole("heading", { name: "Usuarios y grupos" }),
    ).toBeInTheDocument();
    const sidebar = screen.getByLabelText("Navegacion principal");

    expect(within(sidebar).getByText("Sesión activa")).toBeInTheDocument();
    expect(within(sidebar).getByText("ana@example.com")).toBeInTheDocument();
    expect(within(sidebar).getByText("Admin")).toBeInTheDocument();
    expect(
      within(sidebar).getByRole("button", { name: "Cerrar sesión" }),
    ).toBeInTheDocument();
  });

  test("shows users with roles, groups, status, budget, spend, and remaining budget", async () => {
    stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, [
        { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
      ]),
    ]);

    render(<App />);

    expect(
      await screen.findByRole("heading", { name: "Usuarios y grupos" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Usuarios" })).toHaveAttribute(
      "aria-selected",
      "true",
    );
    expect(screen.getByRole("tab", { name: "Grupos" })).toHaveAttribute(
      "aria-selected",
      "false",
    );
    expect(
      screen.queryByRole("button", { name: "Crear grupo" }),
    ).not.toBeInTheDocument();
    const row = screen.getByRole("row", { name: /Ana Gomez/i });

    expect(within(row).getByText("ana@example.com")).toBeInTheDocument();
    expect(within(row).getByText("Admin")).toBeInTheDocument();
    expect(within(row).getByText("Operaciones")).toBeInTheDocument();
    expect(within(row).getByText("Activo")).toBeInTheDocument();
    expect(within(row).getByText("USD 5.00")).toBeInTheDocument();
    expect(within(row).getByText("USD 1.25")).toBeInTheDocument();
    expect(within(row).getByText("USD 3.75")).toBeInTheDocument();
  });

  test("adds tooltips to user action buttons", async () => {
    stubFetch([jsonResponse(200, usersResponse), jsonResponse(200, [])]);

    render(<App />);

    expect(
      await screen.findByRole("heading", { name: "Usuarios y grupos" }),
    ).toBeInTheDocument();

    expectActionTooltip(
      screen.getByRole("button", { name: "Actualizar usuarios" }),
    );
    expectActionTooltip(
      screen.getByRole("button", { name: "Editar presupuesto de Ana Gomez" }),
      "Editar presupuesto",
    );
    expectActionTooltip(
      screen.getByRole("button", { name: "Dar de baja a Ana Gomez" }),
      "Dar de baja",
    );
  });

  test("filters users by search text and active status", async () => {
    const mixedUsers = [
      usersResponse[0],
      {
        id: "33333333-3333-3333-3333-333333333333",
        email: "bruno@example.com",
        displayName: "Bruno Perez",
        isActive: false,
        roles: ["Viewer"],
        groups: [
          { id: "44444444-4444-4444-4444-444444444444", name: "Ventas" },
        ],
        accessScopeHash: "other-scope-hash",
        monthlyBudgetUsd: 5,
        currentSpendUsd: 0,
        remainingBudgetUsd: 5,
        isBudgetDisabled: false,
      },
    ];
    stubFetch([
      jsonResponse(200, mixedUsers),
      jsonResponse(200, [
        { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
        { id: "44444444-4444-4444-4444-444444444444", name: "Ventas" },
      ]),
    ]);
    const user = userEvent.setup();

    render(<App />);

    expect(await screen.findByText("Ana Gomez")).toBeInTheDocument();
    expect(screen.getByText("Bruno Perez")).toBeInTheDocument();

    await user.type(
      screen.getByRole("searchbox", { name: "Buscar usuarios" }),
      "ventas",
    );

    expect(screen.queryByText("Ana Gomez")).not.toBeInTheDocument();
    expect(screen.getByText("Bruno Perez")).toBeInTheDocument();

    await user.selectOptions(
      screen.getByRole("combobox", { name: "Estado" }),
      "active",
    );

    expect(screen.queryByText("Ana Gomez")).not.toBeInTheDocument();
    expect(screen.queryByText("Bruno Perez")).not.toBeInTheDocument();
    expect(
      screen.getByText("No hay usuarios que coincidan con los filtros."),
    ).toBeInTheDocument();
  });

  test("validates non-negative budget values in the edit dialog", async () => {
    stubFetch([jsonResponse(200, usersResponse), jsonResponse(200, [])]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(
      await screen.findByRole("button", {
        name: "Editar presupuesto de Ana Gomez",
      }),
    );
    const dialog = await screen.findByRole("dialog", { name: "Editar presupuesto" });
    expect(
      within(dialog).getByText("Ajustá el límite mensual o deshabilitá el control de gasto para este usuario."),
    ).toBeInTheDocument();
    expect(within(dialog).queryByRole("button", { name: "Cerrar" })).not.toBeInTheDocument();
    const input = screen.getByRole("spinbutton", {
      name: "Presupuesto mensual (USD)",
    });
    await user.clear(input);
    await user.type(input, "-1");
    await user.click(screen.getByRole("button", { name: "Guardar" }));

    expect(
      screen.getByText("El presupuesto no puede ser negativo."),
    ).toBeInTheDocument();
  });

  test("saves a budget change and shows a success state", async () => {
    const updatedUser = {
      ...usersResponse[0],
      monthlyBudgetUsd: 7,
      remainingBudgetUsd: 5.75,
    };
    const fetchMock = stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      csrfResponse(),
      jsonResponse(200, updatedUser),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(
      await screen.findByRole("button", {
        name: "Editar presupuesto de Ana Gomez",
      }),
    );
    const input = screen.getByRole("spinbutton", {
      name: "Presupuesto mensual (USD)",
    });
    await user.clear(input);
    await user.type(input, "7");
    await user.click(screen.getByRole("button", { name: "Guardar" }));

    expect(
      await screen.findByText("Presupuesto actualizado."),
    ).toBeInTheDocument();
    expect(screen.getByText("USD 7.00")).toBeInTheDocument();
    expect(fetchMock).toHaveBeenLastCalledWith(
      "/api/users/11111111-1111-1111-1111-111111111111/ai-budget",
      expect.objectContaining({ method: "PUT" }),
    );
  });

  test("deactivates a user with a logical delete action", async () => {
    const inactiveUser = { ...usersResponse[0], isActive: false };
    const fetchMock = stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      csrfResponse(),
      jsonResponse(200, inactiveUser),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(
      await screen.findByRole("button", { name: "Dar de baja a Ana Gomez" }),
    );

    expect(
      await screen.findByText("Usuario dado de baja."),
    ).toBeInTheDocument();
    expect(screen.getByText("Inactivo")).toBeInTheDocument();
    expect(fetchMock).toHaveBeenLastCalledWith(
      "/api/users/11111111-1111-1111-1111-111111111111/status",
      expect.objectContaining({ method: "PATCH" }),
    );
  });

  test("creates a group from the management UI", async () => {
    const fetchMock = stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      csrfResponse(),
      jsonResponse(201, {
        id: "33333333-3333-3333-3333-333333333333",
        name: "Ventas",
      }),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(await screen.findByRole("tab", { name: "Grupos" }));
    expect(screen.getByRole("tab", { name: "Grupos" })).toHaveAttribute(
      "aria-selected",
      "true",
    );
    await user.click(
      await screen.findByRole("button", { name: "Crear grupo" }),
    );
    await user.type(
      screen.getByRole("textbox", { name: "Nombre del grupo" }),
      "Ventas",
    );
    await user.click(screen.getByRole("button", { name: "Guardar grupo" }));

    expect(await screen.findByText("Ventas")).toBeInTheDocument();
    expect(fetchMock).toHaveBeenLastCalledWith(
      "/api/groups",
      expect.objectContaining({ method: "POST" }),
    );
  });

  test("creates a group with owner unit and publishing policy", async () => {
    const fetchMock = stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      csrfResponse(),
      jsonResponse(201, {
        id: "33333333-3333-3333-3333-333333333333",
        name: "Soporte",
        ownerOrganizationalUnit: organizationalUnitsResponse[0],
        publishingPolicy: "AdminOnly",
      }),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(await screen.findByRole("tab", { name: "Grupos" }));
    await user.click(
      await screen.findByRole("button", { name: "Crear grupo" }),
    );
    await user.type(
      screen.getByRole("textbox", { name: "Nombre del grupo" }),
      "Soporte",
    );
    await selectUnitOption(user, "Unidad dueña", "Empresa (toda la empresa)");
    await user.selectOptions(
      screen.getByRole("combobox", { name: "Política de publicación" }),
      "AdminOnly",
    );
    await user.click(screen.getByRole("button", { name: "Guardar grupo" }));

    expect(await screen.findByText("Soporte")).toBeInTheDocument();
    const [, requestInit] = fetchMock.mock.calls.find(
      ([path, init]) =>
        path === "/api/groups" &&
        typeof init === "object" &&
        init !== null &&
        "method" in init &&
        init.method === "POST",
    )!;
    const body = JSON.parse(requestInit!.body as string);
    expect(body.ownerOrganizationalUnitId).toBe(
      "01000000-0000-0000-0000-000000000001",
    );
    expect(body.publishingPolicy).toBe("AdminOnly");
  });

  test("creates a viewer user with group access from the management UI", async () => {
    const createdViewer = {
      id: "44444444-4444-4444-4444-444444444444",
      email: "viewer@example.com",
      displayName: "Viewer Demo",
      isActive: true,
      roles: ["Viewer"],
      groups: [
        { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
      ],
      accessScopeHash: "viewer-scope",
      monthlyBudgetUsd: 5,
      currentSpendUsd: 0,
      remainingBudgetUsd: 5,
      isBudgetDisabled: false,
    };
    const fetchMock = stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, [
        { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
      ]),
      csrfResponse(),
      jsonResponse(201, createdViewer),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(
      await screen.findByRole("button", { name: "Crear usuario" }),
    );
    await user.type(
      screen.getByRole("textbox", { name: "Email" }),
      "viewer@example.com",
    );
    await user.type(
      screen.getByRole("textbox", { name: "Nombre visible" }),
      "Viewer Demo",
    );
    await user.type(
      screen.getByLabelText("Contraseña temporal"),
      "DemoPassword!42",
    );
    await selectUnitOption(user, "Unidad organizativa", "Empresa (toda la empresa)");
    await user.selectOptions(
      screen.getByRole("combobox", { name: "Rol" }),
      "Viewer",
    );
    await user.click(screen.getByRole("checkbox", { name: "Operaciones" }));
    await user.click(screen.getByRole("button", { name: "Crear usuario" }));

    expect(await screen.findByText("Usuario creado.")).toBeInTheDocument();
    expect(screen.getByText("Viewer Demo")).toBeInTheDocument();
    expect(fetchMock).toHaveBeenLastCalledWith(
      "/api/users",
      expect.objectContaining({ method: "POST" }),
    );
  });

  test("creates a user with one organizational unit and multiple groups", async () => {
    const createdUser = {
      id: "55555555-5555-5555-5555-555555555555",
      email: "editor@example.com",
      displayName: "Editor Demo",
      isActive: true,
      roles: ["DocumentEditor"],
      groups: [
        { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
        { id: "44444444-4444-4444-4444-444444444444", name: "Compras" },
      ],
      organizationalUnit: {
        id: "0c000000-0000-0000-0000-0000000000c0",
        name: "Comunicación",
        parentId: "01000000-0000-0000-0000-000000000001",
        depth: 1,
        isActive: true,
      },
      accessScopeHash: "editor-scope",
      monthlyBudgetUsd: 5,
      currentSpendUsd: 0,
      remainingBudgetUsd: 5,
      isBudgetDisabled: false,
    };
    const fetchMock = stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, [
        { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
        { id: "44444444-4444-4444-4444-444444444444", name: "Compras" },
      ]),
      csrfResponse(),
      jsonResponse(201, createdUser),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(
      await screen.findByRole("button", { name: "Crear usuario" }),
    );
    await user.type(
      screen.getByRole("textbox", { name: "Email" }),
      "editor@example.com",
    );
    await user.type(
      screen.getByRole("textbox", { name: "Nombre visible" }),
      "Editor Demo",
    );
    await user.type(
      screen.getByLabelText("Contraseña temporal"),
      "EditorPass!42",
    );

    await selectUnitOption(user, "Unidad organizativa", "Comunicación", [
      "Empresa",
    ]);
    await user.selectOptions(
      screen.getByRole("combobox", { name: "Rol" }),
      "DocumentEditor",
    );
    await user.click(screen.getByRole("checkbox", { name: "Operaciones" }));
    await user.click(screen.getByRole("checkbox", { name: "Compras" }));
    await user.click(screen.getByRole("button", { name: "Crear usuario" }));

    expect(await screen.findByText("Editor Demo")).toBeInTheDocument();
    const [, requestInit] = fetchMock.mock.calls.find(
      ([path, init]) =>
        path === "/api/users" &&
        typeof init === "object" &&
        init !== null &&
        "method" in init &&
        init.method === "POST",
    )!;
    const body = JSON.parse(requestInit!.body as string);
    expect(body.organizationalUnitId).toBe(
      "0c000000-0000-0000-0000-0000000000c0",
    );
    expect(body.groupIds).toEqual([
      "22222222-2222-2222-2222-222222222222",
      "44444444-4444-4444-4444-444444444444",
    ]);
  });

  test("changes a user's organizational unit from the management dialog", async () => {
    const updatedUser = {
      ...usersResponse[0],
      organizationalUnit: organizationalUnitsResponse[1],
    };
    const fetchMock = stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, [
        { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
      ]),
      csrfResponse(),
      jsonResponse(200, updatedUser),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(
      await screen.findByRole("button", { name: "Editar usuario Ana Gomez" }),
    );

    await selectUnitOption(user, "Unidad organizativa", "Comunicación", [
      "Empresa",
    ]);
    await user.click(screen.getByRole("button", { name: "Guardar usuario" }));

    const [, requestInit] = (
      await vi.waitFor(() => {
        const call = fetchMock.mock.calls.find(
          ([path, init]) =>
            path ===
              "/api/users/11111111-1111-1111-1111-111111111111/organizational-unit" &&
            typeof init === "object" &&
            init !== null &&
            "method" in init &&
            init.method === "PUT",
        );
        if (!call) {
          throw new Error("Organizational-unit PUT not issued yet.");
        }
        return call;
      })
    );
    const body = JSON.parse(requestInit!.body as string);
    expect(body.organizationalUnitId).toBe(
      "0c000000-0000-0000-0000-0000000000c0",
    );
  });

  test("shows a safe API error state when saving a budget fails", async () => {
    stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      csrfResponse(),
      jsonResponse(
        400,
        {
          error: {
            code: "VALIDATION_FAILED",
            message: "Monthly budget must be non-negative.",
            details: { field: "monthlyBudgetUsd" },
            requestId: "request-123",
          },
        },
        { "X-Request-ID": "request-123" },
      ),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(
      await screen.findByRole("button", {
        name: "Editar presupuesto de Ana Gomez",
      }),
    );
    const input = screen.getByRole("spinbutton", {
      name: "Presupuesto mensual (USD)",
    });
    await user.clear(input);
    await user.type(input, "9");
    await user.click(screen.getByRole("button", { name: "Guardar" }));

    expect(
      await screen.findByText(
        "No se pudo actualizar el presupuesto. Referencia: request-123.",
      ),
    ).toBeInTheDocument();
  });
});

describe("management documents", () => {
  test("switches the document workspace static copy to English", async () => {
    stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, [
        { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
      ]),
      jsonResponse(200, documentsResponse),
      jsonResponse(200, [
        { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
      ]),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.selectOptions(await screen.findByLabelText("Idioma"), "en-US");
    await user.click(await screen.findByRole("link", { name: "Documents" }));

    expect(await screen.findByRole("heading", { name: "Documents" })).toBeInTheDocument();
    expect(screen.getByLabelText("Document filters")).toBeInTheDocument();
    expect(screen.getByRole("combobox", { name: "Document state" })).toBeInTheDocument();
    expect(screen.getByText("Indexing pending")).toBeInTheDocument();
    expect(screen.getByText("Draft 1 / Published -")).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Edit Politica de seguridad" }),
    ).toBeInTheDocument();
    expect(screen.queryByText("Filtros de documentos")).not.toBeInTheDocument();
    expect(screen.queryByText("Estado del documento")).not.toBeInTheDocument();
    expect(screen.queryByText("Indexacion pendiente")).not.toBeInTheDocument();
  });

  test("documents access column shows company-wide rules", async () => {
    const companyWideDocuments = [
      {
        ...documentsResponse[0],
        allowedGroupIds: [],
        accessRules: [
          {
            id: "r1",
            organizationalUnitId: "01000000-0000-0000-0000-000000000001",
            groupIds: [],
          },
        ],
      },
    ];
    stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, [
        { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
      ]),
      jsonResponse(200, companyWideDocuments),
      jsonResponse(200, [
        { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
      ]),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(await screen.findByRole("link", { name: "Documentos" }));

    const table = await screen.findByRole("table");
    expect(within(table).getByText(/toda la empresa/i)).toBeInTheDocument();
  });

  test("documents access column shows the unit name for unit-scoped rules", async () => {
    const unitScopedDocuments = [
      {
        ...documentsResponse[0],
        allowedGroupIds: [],
        accessRules: [
          {
            id: "r1",
            organizationalUnitId: "0c000000-0000-0000-0000-0000000000c0",
            groupIds: [],
          },
        ],
      },
    ];
    stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, [
        { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
      ]),
      jsonResponse(200, unitScopedDocuments),
      jsonResponse(200, [
        { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
      ]),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(await screen.findByRole("link", { name: "Documentos" }));

    const table = await screen.findByRole("table");
    expect(within(table).getByText("Comunicación")).toBeInTheDocument();
    expect(
      within(table).queryByText(/toda la empresa/i),
    ).not.toBeInTheDocument();

    // The unit filter keeps documents whose rules reference the selected unit
    // and hides the rest.
    await user.selectOptions(
      screen.getByRole("combobox", { name: "Unidad" }),
      "0c000000-0000-0000-0000-0000000000c0",
    );
    expect(screen.getByText("Politica de seguridad")).toBeInTheDocument();

    await user.selectOptions(
      screen.getByRole("combobox", { name: "Unidad" }),
      "01000000-0000-0000-0000-000000000001",
    );
    expect(
      screen.getByText("No hay documentos que coincidan con los filtros."),
    ).toBeInTheDocument();
  });

  test("shows document filters for searchable document attributes", async () => {
    stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, [
        { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
        { id: "44444444-4444-4444-4444-444444444444", name: "Compras" },
      ]),
      jsonResponse(200, documentsResponse),
      jsonResponse(200, [
        { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
        { id: "44444444-4444-4444-4444-444444444444", name: "Compras" },
      ]),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(await screen.findByRole("link", { name: "Documentos" }));

    expect(
      await screen.findByText("Politica de seguridad"),
    ).toBeInTheDocument();
    expect(screen.getByText("Procedimiento de compras")).toBeInTheDocument();
    expect(screen.getByText("Indexacion pendiente")).toBeInTheDocument();

    await user.type(
      screen.getByRole("searchbox", { name: "Buscar documentos" }),
      "compras",
    );

    expect(screen.queryByText("Politica de seguridad")).not.toBeInTheDocument();
    expect(screen.getByText("Procedimiento de compras")).toBeInTheDocument();

    await user.clear(
      screen.getByRole("searchbox", { name: "Buscar documentos" }),
    );
    await user.selectOptions(
      screen.getByRole("combobox", { name: "Estado del documento" }),
      "Draft",
    );

    expect(screen.getByText("Politica de seguridad")).toBeInTheDocument();
    expect(
      screen.queryByText("Procedimiento de compras"),
    ).not.toBeInTheDocument();

    await user.selectOptions(
      screen.getByRole("combobox", { name: "Estado del documento" }),
      "all",
    );
    await user.selectOptions(
      screen.getByRole("combobox", { name: "Indexacion" }),
      "Pending",
    );

    expect(screen.queryByText("Politica de seguridad")).not.toBeInTheDocument();
    expect(screen.getByText("Procedimiento de compras")).toBeInTheDocument();

    await user.selectOptions(
      screen.getByRole("combobox", { name: "Tipo" }),
      "Procedimiento",
    );
    await user.selectOptions(
      screen.getByRole("combobox", { name: "Grupo" }),
      "44444444-4444-4444-4444-444444444444",
    );

    expect(screen.getByText("Procedimiento de compras")).toBeInTheDocument();
  });

  test("adds tooltips to document row and editor action buttons", async () => {
    stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, [
        { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
        { id: "44444444-4444-4444-4444-444444444444", name: "Compras" },
      ]),
      jsonResponse(200, documentsResponse),
      jsonResponse(200, [
        { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
        { id: "44444444-4444-4444-4444-444444444444", name: "Compras" },
      ]),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(await screen.findByRole("link", { name: "Documentos" }));

    expectActionTooltip(
      await screen.findByRole("button", { name: "Actualizar documentos" }),
    );
    expectActionTooltip(
      screen.getByRole("button", { name: "Editar Politica de seguridad" }),
      "Editar documento",
    );
    expectActionTooltip(
      screen.getByRole("button", {
        name: "Abrir visor de Politica de seguridad",
      }),
      "Abrir visor",
    );
    expectActionTooltip(
      screen.getByRole("button", { name: "Archivar Politica de seguridad" }),
      "Archivar documento",
    );

    await user.click(screen.getByRole("button", { name: "Crear documento" }));

    expectActionTooltip(await screen.findByRole("button", { name: "Negrita" }));
    expectActionTooltip(screen.getByRole("button", { name: "Insertar tabla" }));
    expectActionTooltip(
      screen.getByRole("button", { name: "Insertar imagen" }),
    );
  });

  test("creates a document with the full-screen TipTap editor and assigned groups", async () => {
    const createdDocument = {
      ...documentDetail,
      id: "99999999-9999-9999-9999-999999999999",
      title: "Nueva instruccion",
      currentDraftVersion: {
        ...documentDetail.currentDraftVersion!,
        title: "Nueva instruccion",
        documentType: "Procedimiento",
        contentHtml: "<p>Usar casco visible.</p>",
      },
    };
    const fetchMock = stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, [
        { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
      ]),
      jsonResponse(200, documentsResponse),
      jsonResponse(200, [
        { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
      ]),
      csrfResponse(),
      jsonResponse(201, createdDocument),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(await screen.findByRole("link", { name: "Documentos" }));
    await user.click(
      await screen.findByRole("button", { name: "Crear documento" }),
    );

    expect(
      await screen.findByRole("heading", { name: "Crear documento" }),
    ).toBeInTheDocument();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Editor" })).toHaveAttribute(
      "aria-selected",
      "true",
    );
    expect(screen.getByRole("tab", { name: "Listado" })).toHaveAttribute(
      "aria-selected",
      "false",
    );
    expect(
      screen.getByRole("button", { name: "Volver al listado" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Negrita" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Cursiva" })).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Subrayado" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Tachado" })).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Lista con vinetas" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Lista numerada" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Cita" })).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Codigo en linea" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Bloque de codigo" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Linea horizontal" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Deshacer" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Rehacer" })).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Insertar tabla" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Insertar imagen" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("combobox", { name: "Estilo de bloque" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Parrafo" })).not.toBeInTheDocument();
    expect(screen.queryByRole("combobox", { name: "Color de texto" })).not.toBeInTheDocument();
    expect(screen.getByLabelText("Color de texto")).toHaveAttribute("type", "color");

    await user.type(
      screen.getByRole("textbox", { name: "Titulo" }),
      "Nueva instruccion",
    );
    await user.selectOptions(
      screen.getByRole("combobox", { name: "Tipo" }),
      screen.getByRole("option", { name: "Procedimiento" }),
    );
    await user.click(screen.getByRole("checkbox", { name: "Operaciones" }));
    await user.type(
      screen.getByRole("textbox", { name: "Contenido del documento" }),
      "Usar casco visible.",
    );
    await user.click(screen.getByRole("button", { name: "Guardar borrador" }));

    expect(await screen.findByText("Documento creado.")).toBeInTheDocument();
    expect(fetchMock).toHaveBeenLastCalledWith(
      "/api/documents",
      expect.objectContaining({ method: "POST" }),
    );
  }, 15000);

  test("creates an access group from the document editor modal and adds it to the rule", async () => {
    const newGroup = {
      id: "33333333-3333-3333-3333-333333333333",
      name: "Mantenimiento",
    };
    const createdDocument = {
      ...documentDetail,
      id: "99999999-9999-9999-9999-999999999999",
      title: "Documento con grupo nuevo",
      allowedGroupIds: [newGroup.id],
      currentDraftVersion: {
        ...documentDetail.currentDraftVersion!,
        title: "Documento con grupo nuevo",
        documentType: "Procedimiento",
        contentHtml: "<p>Contenido operativo.</p>",
      },
    };
    const fetchMock = stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      jsonResponse(200, documentsResponse),
      jsonResponse(200, []),
      csrfResponse(),
      jsonResponse(201, newGroup),
      csrfResponse(),
      jsonResponse(201, createdDocument),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(await screen.findByRole("link", { name: "Documentos" }));
    await user.click(
      await screen.findByRole("button", { name: "Crear documento" }),
    );

    expect(
      screen.queryByRole("textbox", { name: "Nombre del grupo" }),
    ).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Nuevo grupo" }));

    const dialog = await screen.findByRole("dialog", { name: "Crear grupo" });
    await user.type(
      within(dialog).getByRole("textbox", { name: "Nombre del grupo" }),
      newGroup.name,
    );
    await user.click(within(dialog).getByRole("button", { name: "Guardar grupo" }));

    // The created group is added to the first access rule and checked.
    expect(
      await screen.findByRole("checkbox", { name: newGroup.name }),
    ).toBeChecked();
    expect(screen.getByText("Grupo Mantenimiento creado.")).toBeInTheDocument();
    expect(screen.queryByRole("dialog", { name: "Crear grupo" })).not.toBeInTheDocument();

    await user.type(
      screen.getByRole("textbox", { name: "Titulo" }),
      "Documento con grupo nuevo",
    );
    await user.selectOptions(
      screen.getByRole("combobox", { name: "Tipo" }),
      screen.getByRole("option", { name: "Procedimiento" }),
    );
    await user.type(
      screen.getByRole("textbox", { name: "Contenido del documento" }),
      "Contenido operativo.",
    );
    await user.click(screen.getByRole("button", { name: "Guardar borrador" }));

    expect(await screen.findByText("Documento creado.")).toBeInTheDocument();
    const [, requestInit] = fetchMock.mock.calls.find(
      ([path, init]) =>
        path === "/api/documents" &&
        typeof init === "object" &&
        init !== null &&
        "method" in init &&
        init.method === "POST",
    )!;
    expect(JSON.parse(requestInit!.body as string).accessRules).toEqual([
      { organizationalUnitId: null, groupIds: [newGroup.id] },
    ]);
  }, 15000);

  test("document editor saves organizational-unit and group access rules", async () => {
    const createdDocument = {
      ...documentDetail,
      id: "99999999-9999-9999-9999-999999999999",
      title: "Protocolo de crisis",
      allowedGroupIds: ["44444444-4444-4444-4444-444444444444"],
      currentDraftVersion: {
        ...documentDetail.currentDraftVersion!,
        title: "Protocolo de crisis",
        documentType: "Procedimiento",
        contentHtml: "<p>Escalar al comité.</p>",
      },
    };
    const fetchMock = stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      jsonResponse(200, documentsResponse),
      jsonResponse(200, [
        { id: "44444444-4444-4444-4444-444444444444", name: "Comité de crisis" },
      ]),
      csrfResponse(),
      jsonResponse(201, createdDocument),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(await screen.findByRole("link", { name: "Documentos" }));
    await user.click(
      await screen.findByRole("button", { name: "Crear documento" }),
    );

    await user.type(
      screen.getByRole("textbox", { name: "Titulo" }),
      "Protocolo de crisis",
    );
    await user.selectOptions(
      screen.getByRole("combobox", { name: "Tipo" }),
      screen.getByRole("option", { name: "Procedimiento" }),
    );

    await selectUnitOption(user, "Unidad organizativa", "Comunicación", [
      "Empresa",
    ]);
    await user.click(screen.getByRole("checkbox", { name: "Comité de crisis" }));

    // The rule card explains the AND/OR semantics in natural language.
    expect(
      screen.getByText(
        "Acceden: usuarios de la rama de Comunicación que además pertenezcan a Comité de crisis.",
      ),
    ).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Guardar borrador" }));

    expect(await screen.findByText("Documento creado.")).toBeInTheDocument();
    const [, requestInit] = fetchMock.mock.calls.find(
      ([path, init]) =>
        path === "/api/documents" &&
        typeof init === "object" &&
        init !== null &&
        "method" in init &&
        init.method === "POST",
    )!;
    expect(JSON.parse(requestInit!.body as string).accessRules).toEqual([
      {
        organizationalUnitId: "0c000000-0000-0000-0000-0000000000c0",
        groupIds: ["44444444-4444-4444-4444-444444444444"],
      },
    ]);
  }, 15000);

  test("document editor surfaces invalid empty access rule errors", async () => {
    const fetchMock = stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      jsonResponse(200, documentsResponse),
      jsonResponse(200, []),
      csrfResponse(),
      jsonResponse(400, {
        error: {
          code: "VALIDATION_FAILED",
          message: "Document access rules are invalid.",
          details: { field: "accessRules" },
          requestId: "req-rules",
        },
      }),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(await screen.findByRole("link", { name: "Documentos" }));
    await user.click(
      await screen.findByRole("button", { name: "Crear documento" }),
    );

    await user.type(
      screen.getByRole("textbox", { name: "Titulo" }),
      "Sin reglas",
    );
    await user.selectOptions(
      screen.getByRole("combobox", { name: "Tipo" }),
      screen.getByRole("option", { name: "Procedimiento" }),
    );

    // The default access rule is left empty (no unit, no group); the backend
    // rejects it and the editor surfaces the Spanish validation error.
    await user.click(screen.getByRole("button", { name: "Guardar borrador" }));

    expect(
      await screen.findByText(
        "Revisá las reglas de acceso: cada regla necesita una unidad o un grupo y no puede quedar vacía.",
      ),
    ).toBeInTheDocument();
    expect(fetchMock).toHaveBeenLastCalledWith(
      "/api/documents",
      expect.objectContaining({ method: "POST" }),
    );
  }, 15000);

  test("exposes the richer TipTap toolbar", async () => {
    stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      jsonResponse(200, documentsResponse),
      jsonResponse(200, []),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(await screen.findByRole("link", { name: "Documentos" }));
    await user.click(
      await screen.findByRole("button", { name: "Crear documento" }),
    );

    expect(screen.getByRole("combobox", { name: "Estilo de bloque" })).toBeInTheDocument();
    expect(screen.getByLabelText("Color de texto")).toHaveAttribute("type", "color");

    [
      "Negrita",
      "Cursiva",
      "Subrayado",
      "Tachado",
      "Lista con vinetas",
      "Lista numerada",
      "Cita",
      "Codigo en linea",
      "Bloque de codigo",
      "Linea horizontal",
      "Deshacer",
      "Rehacer",
      "Enlace",
      "Insertar tabla",
      "Insertar imagen",
    ].forEach((label) => {
      expectActionTooltip(screen.getByRole("button", { name: label }));
    });
  });

  test("opens editor, tracks dirty state, and sends a valid draft to review", async () => {
    const fetchMock = stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, [
        { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
      ]),
      jsonResponse(200, documentsResponse),
      jsonResponse(200, [
        { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
      ]),
      jsonResponse(200, documentDetail),
      csrfResponse(),
      jsonResponse(200, { ...documentDetail, title: "Politica actualizada" }),
      csrfResponse(),
      jsonResponse(200, { ...documentDetail, state: "In Review" }),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(await screen.findByRole("link", { name: "Documentos" }));
    await user.click(
      await screen.findByRole("button", {
        name: "Editar Politica de seguridad",
      }),
    );
    await user.clear(screen.getByRole("textbox", { name: "Titulo" }));
    await user.type(
      screen.getByRole("textbox", { name: "Titulo" }),
      "Politica actualizada",
    );

    expect(screen.getByText("Cambios sin guardar")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Guardar borrador" }));
    expect(await screen.findByText("Borrador guardado.")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Enviar a revision" }));
    expect(
      await screen.findByText("Documento enviado a revision."),
    ).toBeInTheDocument();
    expect(fetchMock).toHaveBeenLastCalledWith(
      "/api/documents/55555555-5555-5555-5555-555555555555/send-to-review",
      expect.objectContaining({ method: "POST" }),
    );
  });

  test("shows review validation errors before send to review", async () => {
    stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      jsonResponse(200, documentsResponse),
      jsonResponse(200, []),
      jsonResponse(200, {
        ...documentDetail,
        allowedGroupIds: [],
        currentDraftVersion: {
          ...documentDetail.currentDraftVersion!,
          title: "",
          contentHtml: "",
        },
      }),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(await screen.findByRole("link", { name: "Documentos" }));
    await user.click(
      await screen.findByRole("button", {
        name: "Editar Politica de seguridad",
      }),
    );
    await user.click(
      await screen.findByRole("button", { name: "Enviar a revision" }),
    );

    expect(
      screen.getByText(
        "Completa titulo, tipo, reglas de acceso y contenido antes de enviar a revision.",
      ),
    ).toBeInTheDocument();
    expect(screen.getByRole("textbox", { name: "Titulo" })).toHaveAttribute(
      "aria-invalid",
      "true",
    );
    expect(screen.getByRole("combobox", { name: "Tipo" })).toHaveAttribute(
      "aria-invalid",
      "false",
    );
  });

  test("shows publish instead of send to review for admins when a document is in review", async () => {
    const fetchMock = stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, [
        { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
        { id: "44444444-4444-4444-4444-444444444444", name: "Compras" },
      ]),
      jsonResponse(200, documentsResponse),
      jsonResponse(200, [
        { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
        { id: "44444444-4444-4444-4444-444444444444", name: "Compras" },
      ]),
      jsonResponse(200, inReviewDocumentDetail),
      csrfResponse(),
      jsonResponse(200, {
        ...inReviewDocumentDetail,
        state: "Published",
        currentDraftVersion: null,
      }),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(await screen.findByRole("link", { name: "Documentos" }));
    await user.click(
      await screen.findByRole("button", {
        name: "Editar Procedimiento de compras",
      }),
    );

    expect(
      await screen.findByRole("button", { name: "Publicar" }),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Enviar a revision" }),
    ).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Publicar" }));

    expect(await screen.findByText("Documento publicado.")).toBeInTheDocument();
    expect(fetchMock).toHaveBeenLastCalledWith(
      "/api/documents/66666666-6666-6666-6666-666666666666/request-publish",
      expect.objectContaining({ method: "POST" }),
    );
  });

  test("opens published version content in the editor when no draft exists", async () => {
    stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, [
        { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
      ]),
      jsonResponse(200, [
        {
          id: publishedDocumentDetail.id,
          title: publishedDocumentDetail.title,
          state: "Published",
          documentType: "Manual",
          allowedGroupIds: ["22222222-2222-2222-2222-222222222222"],
          draftVersionNumber: null,
          publishedVersionNumber: 1,
          indexingStatus: "Succeeded",
          updatedAt: publishedDocumentDetail.updatedAt,
        },
      ]),
      jsonResponse(200, [
        { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
      ]),
      jsonResponse(200, publishedDocumentDetail),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(await screen.findByRole("link", { name: "Documentos" }));
    await user.click(
      await screen.findByRole("button", { name: "Editar Manual publicado" }),
    );

    expect(
      await screen.findByRole("heading", { name: "Editar documento" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("textbox", { name: "Titulo" })).toHaveValue(
      "Manual publicado",
    );
    expect(screen.getByRole("combobox", { name: "Tipo" })).toHaveDisplayValue(
      "Manual",
    );
    expect(
      screen.getByRole("textbox", { name: "Contenido del documento" }),
    ).toHaveTextContent("Contenido vigente publicado.");
    expect(
      screen.getByRole("button", { name: "Guardar borrador" }),
    ).toBeInTheDocument();
  });

  test("hides publish and send-to-review actions from document editors when a document is in review", async () => {
    stubFetch(
      [
        jsonResponse(200, usersResponse),
        jsonResponse(200, [
          { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
          { id: "44444444-4444-4444-4444-444444444444", name: "Compras" },
        ]),
        jsonResponse(200, documentsResponse),
        jsonResponse(200, [
          { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
          { id: "44444444-4444-4444-4444-444444444444", name: "Compras" },
        ]),
        jsonResponse(200, inReviewDocumentDetail),
      ],
      { session: documentEditorSessionUser },
    );
    const user = userEvent.setup();

    render(<App />);

    await user.click(await screen.findByRole("link", { name: "Documentos" }));
    await user.click(
      await screen.findByRole("button", {
        name: "Editar Procedimiento de compras",
      }),
    );

    expect(
      await screen.findByRole("heading", { name: "Editar documento" }),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Publicar" }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Enviar a revision" }),
    ).not.toBeInTheDocument();
  });

  test("imports text content, sets the title from the filename, and shows safe import errors", async () => {
    stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      jsonResponse(200, documentsResponse),
      jsonResponse(200, []),
      jsonResponse(200, documentDetail),
      csrfResponse(),
      jsonResponse(200, {
        text: "Contenido importado plano",
        contentHtml: "<h1>Procedimiento importado</h1><ul><li>Paso importado</li></ul>",
        metadata: {
          originalFilename: "manual.pdf",
          mimeType: "application/pdf",
          sizeBytes: 2048,
          sha256Hash: "hash",
          extractionStatus: "Extracted",
        },
      }),
      csrfResponse(),
      jsonResponse(422, {
        error: {
          code: "IMPORT_TEXT_NOT_EXTRACTABLE",
          message: "Uploaded file has no extractable text.",
          details: null,
          requestId: "request-import",
        },
      }),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(await screen.findByRole("link", { name: "Documentos" }));
    await user.click(
      await screen.findByRole("button", {
        name: "Editar Politica de seguridad",
      }),
    );

    expect(screen.getByText("Seleccionar archivo")).toBeInTheDocument();

    // A PDF import prefills the editor in-form and sets the title from the filename.
    await user.upload(
      await screen.findByLabelText("Importar PDF o DOCX"),
      new File(["pdf"], "manual.pdf", { type: "application/pdf" }),
    );

    expect(
      await screen.findByRole("textbox", { name: "Contenido del documento" }),
    ).toHaveTextContent("Procedimiento importado");
    expect(
      screen.getByRole("textbox", { name: "Contenido del documento" }),
    ).toHaveTextContent("Paso importado");
    expect(
      screen.getByText("Texto importado desde manual.pdf."),
    ).toBeInTheDocument();
    expect(screen.getByDisplayValue("manual")).toBeInTheDocument();

    await user.upload(
      screen.getByLabelText("Importar PDF o DOCX"),
      new File(["pdf"], "scan.pdf", { type: "application/pdf" }),
    );

    expect(
      await screen.findByText(
        "No se pudo extraer texto del archivo. Referencia: request-import.",
      ),
    ).toBeInTheDocument();
  });

  test("archives and restores documents from the list", async () => {
    const fetchMock = stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      jsonResponse(200, [
        documentsResponse[0],
        {
          ...documentsResponse[1],
          id: "88888888-8888-8888-8888-888888888888",
          state: "Archived",
        },
      ]),
      jsonResponse(200, []),
      csrfResponse(),
      jsonResponse(200, { ...documentDetail, state: "Archived" }),
      csrfResponse(),
      jsonResponse(200, {
        ...documentDetail,
        id: "88888888-8888-8888-8888-888888888888",
        state: "Draft",
      }),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(await screen.findByRole("link", { name: "Documentos" }));
    await user.click(
      await screen.findByRole("button", {
        name: "Archivar Politica de seguridad",
      }),
    );
    expect(await screen.findByText("Documento archivado.")).toBeInTheDocument();

    await user.click(
      screen.getByRole("button", {
        name: "Restaurar Procedimiento de compras",
      }),
    );
    expect(
      await screen.findByText("Documento restaurado."),
    ).toBeInTheDocument();
    expect(fetchMock).toHaveBeenLastCalledWith(
      "/api/documents/88888888-8888-8888-8888-888888888888/restore",
      expect.objectContaining({ method: "POST" }),
    );
  });

  test("opens management document viewer through exchange links", async () => {
    const assign = vi.fn();
    const open = vi.fn();
    vi.stubGlobal("open", open);
    Object.defineProperty(window, "location", {
      configurable: true,
      value: { assign },
    });
    const fetchMock = stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      jsonResponse(200, documentsResponse),
      jsonResponse(200, []),
      csrfResponse(),
      jsonResponse(200, {
        url: "https://docs.client.com/open?documentId=55555555-5555-5555-5555-555555555555",
        expiresAt: null,
      }),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(await screen.findByRole("link", { name: "Documentos" }));
    await user.click(
      await screen.findByRole("button", {
        name: "Abrir visor de Politica de seguridad",
      }),
    );

    expect(fetchMock).toHaveBeenLastCalledWith(
      "/api/viewer/links",
      expect.objectContaining({ method: "POST" }),
    );
    expect(assign).not.toHaveBeenCalled();
    expect(open).toHaveBeenCalledWith(
      "https://docs.client.com/open?documentId=55555555-5555-5555-5555-555555555555",
      "_blank",
      "noopener,noreferrer",
    );
  });

  test("retries failed indexing from the document list", async () => {
    const fetchMock = stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      jsonResponse(200, [
        {
          ...documentsResponse[0],
          indexingStatus: "Failed",
          state: "In Review",
        },
      ]),
      jsonResponse(200, []),
      csrfResponse(),
      jsonResponse(200, {
        ...documentDetail,
        state: "Published",
        currentDraftVersion: {
          ...documentDetail.currentDraftVersion,
          indexingStatus: "Succeeded",
        },
      }),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(await screen.findByRole("link", { name: "Documentos" }));
    await user.click(
      await screen.findByRole("button", {
        name: "Reintentar indexacion de Politica de seguridad",
      }),
    );

    expect(
      await screen.findByText("Indexacion reintentada."),
    ).toBeInTheDocument();
    expect(fetchMock).toHaveBeenLastCalledWith(
      "/api/documents/55555555-5555-5555-5555-555555555555/request-publish",
      expect.objectContaining({ method: "POST" }),
    );
  });
});

describe("management organizational units", () => {
  test("organizational units page creates a child unit", async () => {
    const createdUnit = {
      id: "0c000000-0000-0000-0000-0000000000cc",
      name: "Marketing",
      parentId: "01000000-0000-0000-0000-000000000001",
      depth: 1,
      isActive: true,
    };
    const fetchMock = stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      jsonResponse(200, organizationalUnitsResponse),
      csrfResponse(),
      jsonResponse(201, createdUnit),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(
      await screen.findByRole("link", { name: /unidades organizativas/i }),
    );
    await user.click(
      (
        await screen.findAllByRole("button", { name: /agregar unidad hija/i })
      )[0],
    );
    await user.type(
      screen.getByLabelText(/nombre de la unidad/i),
      "Marketing",
    );
    await user.click(screen.getByRole("button", { name: /^guardar$/i }));

    expect(await screen.findByText("Marketing")).toBeInTheDocument();
    expect(fetchMock).toHaveBeenLastCalledWith(
      "/api/organizational-units",
      expect.objectContaining({ method: "POST" }),
    );
  });

  test("organizational units page shows a level badge per depth", async () => {
    stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      jsonResponse(200, organizationalUnitsResponse),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(
      await screen.findByRole("link", { name: /unidades organizativas/i }),
    );

    // Root "Empresa" sits at level 0, its child "Comunicación" at level 1.
    const empresaRow = (
      await screen.findByText("Empresa", { exact: false })
    ).closest(".org-unit-row") as HTMLElement;
    const comunicacionRow = screen
      .getByText("Comunicación")
      .closest(".org-unit-row") as HTMLElement;
    expect(within(empresaRow).getByText("N0")).toBeInTheDocument();
    expect(within(comunicacionRow).getByText("N1")).toBeInTheDocument();
  });
});

describe("management configuration", () => {
  test("shows operational defaults and hides secret values", async () => {
    stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      jsonResponse(200, configurationResponse),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(
      await screen.findByRole("link", { name: "Configuración" }),
    );

    expect(
      await screen.findByRole("heading", { name: "Configuracion operativa" }),
    ).toBeInTheDocument();
    expect(
      screen.getByText("America/Argentina/Buenos_Aires"),
    ).toBeInTheDocument();
    expect(screen.getByText("gpt-4.1-nano")).toBeInTheDocument();
    expect(screen.getByText("text-embedding-3-small")).toBeInTheDocument();
    expect(screen.getByText("1024 dimensiones")).toBeInTheDocument();
    expect(screen.getByText("USD 5.00")).toBeInTheDocument();
    expect(screen.getByText("24 horas")).toBeInTheDocument();
    expect(screen.getByText("0.90")).toBeInTheDocument();
    expect(
      screen.getByText("Valores protegidos por secretos"),
    ).toBeInTheDocument();
    expect(screen.queryByText(/sk-/i)).not.toBeInTheDocument();
  });

  test("admin edits and saves operational configuration", async () => {
    const updatedConfiguration = {
      ...configurationResponse,
      chatModel: "gpt-4.1-mini",
    };
    const fetchMock = stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      jsonResponse(200, configurationResponse),
      csrfResponse(),
      jsonResponse(200, updatedConfiguration),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(
      await screen.findByRole("link", { name: "Configuración" }),
    );
    await user.click(await screen.findByRole("button", { name: "Editar" }));

    const chatModelInput = screen.getByLabelText("Modelo de chat");
    await user.clear(chatModelInput);
    await user.type(chatModelInput, "gpt-4.1-mini");

    await user.click(screen.getByRole("button", { name: "Guardar cambios" }));

    expect(
      await screen.findByText("Configuración guardada."),
    ).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/configuration",
      expect.objectContaining({
        method: "PUT",
        body: expect.stringContaining('"chatModel":"gpt-4.1-mini"'),
      }),
    );
  });

  test("viewer sees configuration as read-only without an edit affordance", async () => {
    stubFetch([jsonResponse(200, configurationResponse)], {
      session: viewerSessionUser,
    });
    const user = userEvent.setup();

    render(<App />);

    await user.click(
      await screen.findByRole("link", { name: "Configuración" }),
    );

    expect(
      await screen.findByRole("heading", { name: "Configuracion operativa" }),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Editar" }),
    ).not.toBeInTheDocument();
    expect(
      screen.getByText("Solo un administrador puede editar esta configuración."),
    ).toBeInTheDocument();
  });

  test("shows configuration load errors with a safe state", async () => {
    stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      jsonResponse(500, {
        error: {
          code: "INTERNAL_ERROR",
          message: "An internal error occurred.",
          details: null,
          requestId: "config-request",
        },
      }),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(
      await screen.findByRole("link", { name: "Configuración" }),
    );

    expect(
      await screen.findByText(
        "No se pudo cargar la configuracion. Referencia: config-request.",
      ),
    ).toBeInTheDocument();
  });
});

describe("management feedback reporting", () => {
  test("keeps functional audit separate from feedback review", async () => {
    stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, [
        { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
      ]),
      jsonResponse(200, auditEventsResponse),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(await screen.findByRole("link", { name: "Auditoría" }));

    expect(
      await screen.findByRole("heading", { name: "Auditoria" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("heading", { name: "Eventos funcionales" }),
    ).toBeInTheDocument();
    const auditRow = await screen.findByRole("row", {
      name: /Documento creado/i,
    });
    expect(within(auditRow).getByText("Ana Gomez")).toBeInTheDocument();
    expect(within(auditRow).getByText("req-doc-create")).toBeInTheDocument();
    expect(
      screen.queryByRole("heading", { name: "Feedback" }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("combobox", { name: "Polaridad" }),
    ).not.toBeInTheDocument();
  });

  test("shows empty, filter, and result states for feedback review", async () => {
    stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, [
        { id: "22222222-2222-2222-2222-222222222222", name: "Operaciones" },
      ]),
      jsonResponse(200, []),
      jsonResponse(200, [
        {
          queryAuditEventId: "99999999-9999-9999-9999-999999999999",
          userId: "11111111-1111-1111-1111-111111111111",
          userDisplayName: "Ana Gomez",
          userEmail: "ana.gomez@example.com",
          question: "Que regla aplica?",
          answerSummary: "Usa credencial visible.",
          feedbackValue: "down",
          feedbackComment: "Falto detalle",
          feedbackUpdatedAt: "2026-05-18T12:00:00Z",
          createdAt: "2026-05-18T11:59:00Z",
          cacheHit: false,
          requestId: "req-report",
          citations: [
            {
              documentId: "55555555-5555-5555-5555-555555555555",
              documentVersionId: "77777777-7777-7777-7777-777777777777",
              headingPath: ["Seguridad"],
            },
          ],
        },
        {
          queryAuditEventId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
          userId: "11111111-1111-1111-1111-111111111111",
          userDisplayName: "Ana Gomez",
          userEmail: "ana.gomez@example.com",
          question: "Como ingreso al portal?",
          answerSummary: "Ingresa con tu cuenta corporativa.",
          feedbackValue: null,
          feedbackComment: null,
          feedbackUpdatedAt: null,
          createdAt: "2026-05-18T11:30:00Z",
          cacheHit: true,
          requestId: "req-no-feedback",
          citations: [],
        },
      ]),
    ]);
    const user = userEvent.setup();

    render(<App />);

    await user.click(await screen.findByRole("link", { name: "Feedback" }));

    expect(
      await screen.findByRole("heading", { name: "Feedback" }),
    ).toBeInTheDocument();
    expect(
      await screen.findByText("Todavia no hay preguntas para revisar."),
    ).toBeInTheDocument();
    await user.selectOptions(
      screen.getByRole("combobox", { name: "Polaridad" }),
      "negative",
    );
    await user.click(screen.getByRole("button", { name: "Aplicar filtros" }));

    expect(await screen.findByText("Que regla aplica?")).toBeInTheDocument();
    expect(screen.getByText("Como ingreso al portal?")).toBeInTheDocument();
    expect(screen.getAllByText("ana.gomez@example.com")).toHaveLength(2);
    expect(screen.getByText("No sirvio")).toBeInTheDocument();
    expect(screen.getByText("Sin feedback")).toBeInTheDocument();
    expect(screen.getByText("Falto detalle")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Exportar a Excel" })).toBeInTheDocument();
    expect(screen.queryByText("req-report")).not.toBeInTheDocument();
  });
});

interface StubFetchOptions {
  setupRequired?: boolean;
  session?: typeof sessionUser | null;
}

function stubFetch(responses: Response[], options: StubFetchOptions = {}) {
  const setupRequired = options.setupRequired ?? false;
  const session = options.session === undefined ? sessionUser : options.session;
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const path =
      typeof input === "string"
        ? input
        : input instanceof URL
          ? input.pathname
          : input.url;
    const method = (init?.method ?? "GET").toUpperCase();
    if (path === "/api/setup/status") {
      return jsonResponse(200, {
        setupRequired,
        adminExists: !setupRequired,
        databaseReady: true,
        requiredRoles: ["Admin", "DocumentEditor", "DocumentPublisher", "Viewer"],
      });
    }

    if (path === "/api/session") {
      return session
        ? jsonResponse(200, { user: session })
        : jsonResponse(401, {
            error: {
              code: "AUTH_REQUIRED",
              message: "Authentication required.",
              details: null,
              requestId: "session-request",
            },
          });
    }

    // The organizational-unit tree is fetched lazily by the user dialog and the
    // document editor. Serve GET requests out of band so they never disturb the
    // ordered queue. Mutations (POST/PATCH) still flow through the queue so tests
    // can assert on created/updated units.
    if (path === "/api/organizational-units" && method === "GET") {
      return jsonResponse(200, organizationalUnitsResponse);
    }

    if (path.startsWith("/api/document-types") && method === "GET") {
      return jsonResponse(200, documentTypesResponse);
    }

    const response = responses.shift();
    if (!response) {
      throw new Error(`Unexpected fetch call to ${path}.`);
    }

    return response;
  });

  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

function jsonResponse(
  status: number,
  body: unknown,
  headers: Record<string, string> = {},
) {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      "Content-Type": "application/json",
      ...headers,
    },
  });
}

function csrfResponse() {
  return jsonResponse(200, { status: "ok" }, { "X-CSRF-Token": "csrf-token" });
}

// The organizational-unit dropdowns are a collapsible tree popover: opening the
// trigger shows only the roots, so reaching a child means expanding its
// ancestors (by name) before clicking the option. Every row is a button.
async function selectUnitOption(
  user: ReturnType<typeof userEvent.setup>,
  triggerName: string,
  optionName: string,
  expandBranches: string[] = [],
) {
  await user.click(await screen.findByRole("button", { name: triggerName }));
  for (const branch of expandBranches) {
    await user.click(
      await screen.findByRole("button", { name: `Expandir ${branch}` }),
    );
  }
  await user.click(await screen.findByRole("button", { name: optionName }));
}

function expectActionTooltip(button: HTMLElement, expectedTooltip?: string) {
  const label = button.getAttribute("aria-label");

  expect(label).toBeTruthy();
  expect(button).not.toHaveAttribute("title");
  expect(button).toHaveAttribute("data-tooltip", expectedTooltip ?? label!);
}
