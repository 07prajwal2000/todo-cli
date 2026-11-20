/*
 * Todo dashboard. One state object, one render(), no framework.
 */
(() => {
  'use strict';

  const CLIENT_HEADER = 'X-Todo-Client';
  const CLIENT_VALUE = 'dashboard';
  const FILTERS = [
    { id: 'all', label: 'All' },
    { id: 'pending', label: 'Pending' },
    { id: 'ongoing', label: 'Ongoing' },
    { id: 'backlog', label: 'Backlog' },
    { id: 'completed', label: 'Completed' },
  ];
  const ALLOWED_TRANSITIONS = {
    backlog: ['backlog', 'pending', 'ongoing'],
    pending: ['pending', 'ongoing', 'backlog', 'completed'],
    ongoing: ['ongoing', 'completed', 'pending', 'backlog'],
    completed: ['completed', 'pending', 'ongoing'],
  };
  const STATE_LABELS = {
    backlog: 'Backlog',
    pending: 'Pending',
    ongoing: 'Ongoing',
    completed: 'Completed',
  };

  const state = {
    spaces: [],
    defaultSpace: 'default',
    space: '',
    tasks: [],
    filter: 'all',
    search: '',
    loading: true,
    error: null,
    busy: new Set(),
    remarksOpen: new Set(),
    editor: null,
    confirm: null,
    meta: { provider: '', spreadsheetId: null },
  };

  const dom = {};

  class ApiError extends Error {
    constructor(message, code, fields, status) {
      super(message);
      this.code = code;
      this.fields = fields || null;
      this.status = status;
    }
  }

  // ---------------------------------------------------------------- api

  async function api(path, options) {
    const config = options || {};
    const headers = { Accept: 'application/json', [CLIENT_HEADER]: CLIENT_VALUE };
    const init = { method: config.method || 'GET', headers };

    if (config.body !== undefined) {
      headers['Content-Type'] = 'application/json';
      init.body = JSON.stringify(config.body);
    }

    let response;
    try {
      response = await fetch(path, init);
    } catch (cause) {
      throw new ApiError('Cannot reach the dashboard server. Is it still running?', 'unavailable', null, 0);
    }

    if (response.status === 204) {
      return null;
    }

    const text = await response.text();
    let payload = null;
    if (text) {
      try {
        payload = JSON.parse(text);
      } catch (cause) {
        payload = null;
      }
    }

    if (!response.ok) {
      throw new ApiError(
        (payload && payload.message) || `Request failed (${response.status}).`,
        payload && payload.code,
        payload && payload.fields,
        response.status
      );
    }

    return payload;
  }

  function taskPath(id, space) {
    const query = new URLSearchParams({ space: space || state.space });
    return `/api/tasks/${encodeURIComponent(id)}?${query.toString()}`;
  }

  // -------------------------------------------------------------- toasts

  function toast(message, kind) {
    const variant = { error: 'danger', success: 'success' }[kind] || 'primary';
    const node = document.createElement('div');
    node.className = `toast show border-0 shadow text-bg-${variant}`;
    node.setAttribute('role', kind === 'error' ? 'alert' : 'status');
    node.append(el('div', { class: 'toast-body', text: message }));
    dom.toasts.append(node);

    window.setTimeout(() => {
      node.remove();
    }, kind === 'error' ? 6000 : 3500);
  }

  // --------------------------------------------------------- date helpers

  function localToday() {
    const now = new Date();
    return Date.UTC(now.getFullYear(), now.getMonth(), now.getDate());
  }

  function parseIsoDate(value) {
    if (!value) {
      return null;
    }
    const parts = String(value).split('-');
    if (parts.length !== 3) {
      return null;
    }
    return Date.UTC(Number(parts[0]), Number(parts[1]) - 1, Number(parts[2]));
  }

  const DAY_MS = 86400000;

  function dayDelta(dueOn) {
    const due = parseIsoDate(dueOn);
    return due === null ? null : Math.round((due - localToday()) / DAY_MS);
  }

  function formatDate(value) {
    const stamp = parseIsoDate(value);
    if (stamp === null) {
      return '';
    }
    return new Date(stamp).toLocaleDateString(undefined, {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      timeZone: 'UTC',
    });
  }

  function dueDescription(task) {
    const delta = dayDelta(task.dueOn);
    if (delta === null) {
      return null;
    }

    const absolute = formatDate(task.dueOn);

    if (task.status === 'completed') {
      return { text: `Done · was due ${absolute}`, tone: 'done', absolute };
    }

    if (delta === 0) {
      return { text: 'Due today', tone: 'soon', absolute };
    }
    if (delta === 1) {
      return { text: 'Due tomorrow', tone: 'soon', absolute };
    }
    if (delta > 1) {
      return { text: `Due in ${delta}d`, tone: delta <= 3 ? 'soon' : 'normal', absolute };
    }
    return { text: `${-delta}d overdue`, tone: 'overdue', absolute };
  }

  // ------------------------------------------------------- element helper

  function el(tag, attributes, children) {
    const node = document.createElement(tag);
    const attrs = attributes || {};

    for (const [key, value] of Object.entries(attrs)) {
      if (value === null || value === undefined || value === false) {
        continue;
      }
      if (key === 'text') {
        node.textContent = value;
      } else if (key === 'class') {
        node.className = value;
      } else if (key === 'dataset') {
        for (const [dataKey, dataValue] of Object.entries(value)) {
          node.dataset[dataKey] = dataValue;
        }
      } else if (value === true) {
        node.setAttribute(key, '');
      } else {
        node.setAttribute(key, value);
      }
    }

    for (const child of children || []) {
      if (child === null || child === undefined || child === false) {
        continue;
      }
      node.append(child);
    }

    return node;
  }

  function icon(pathData, size) {
    const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    svg.setAttribute('viewBox', '0 0 16 16');
    svg.setAttribute('width', String(size || 14));
    svg.setAttribute('height', String(size || 14));
    svg.setAttribute('aria-hidden', 'true');
    svg.setAttribute('focusable', 'false');
    const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
    path.setAttribute('d', pathData);
    path.setAttribute('fill', 'none');
    path.setAttribute('stroke', 'currentColor');
    path.setAttribute('stroke-width', '1.7');
    path.setAttribute('stroke-linecap', 'round');
    path.setAttribute('stroke-linejoin', 'round');
    svg.append(path);
    return svg;
  }

  const ICONS = {
    play: 'M5.5 4l7 4-7 4z',
    check: 'M3.5 8.5l3 3 6-7',
    undo: 'M4 7.5h5.5a3 3 0 010 6H6M4 7.5l2.5-2.5M4 7.5L6.5 10',
    pause: 'M6 4.5v7M10 4.5v7',
    edit: 'M3 13h2.5L12.5 6a1.5 1.5 0 00-2.1-2.1L3 11v2z',
    trash: 'M3.5 5h9M6.5 5V3.5h3V5M5 5l.6 8h4.8L11 5',
    clock: 'M8 3.5a4.5 4.5 0 100 9 4.5 4.5 0 000-9zM8 6v2.5l1.8 1.2',
    plus: 'M8 3.5v9M3.5 8h9',
    list: 'M5.5 4.5h7M5.5 8h7M5.5 11.5h7M3 4.5h.01M3 8h.01M3 11.5h.01',
    alert: 'M8 2.5l6 10.5H2L8 2.5zM8 6.5v3M8 11.2h.01',
  };

  const TONE_CLASSES = {
    overdue: 'text-danger',
    soon: 'text-warning-emphasis',
    done: 'text-success',
    normal: 'text-body-secondary',
  };

  const STATE_BADGES = {
    pending: 'bg-primary-subtle text-primary-emphasis',
    ongoing: 'bg-warning-subtle text-warning-emphasis',
    backlog: 'bg-secondary-subtle text-secondary-emphasis',
    completed: 'bg-success-subtle text-success-emphasis',
  };

  // ------------------------------------------------------- data loading

  async function bootstrap() {
    state.loading = true;
    render();

    try {
      const [health, spaces] = await Promise.all([api('/api/health'), api('/api/spaces')]);
      state.meta.provider = (health && health.provider) || '';
      state.meta.spreadsheetId = (health && health.spreadsheetId) || null;
      state.spaces = (spaces && spaces.spaces) || [];
      state.defaultSpace = (spaces && spaces.defaultSpace) || state.defaultSpace;
      state.space = pickInitialSpace(state.spaces);
      await loadTasks();
    } catch (error) {
      state.loading = false;
      state.error = error.message;
      render();
    }
  }

  function pickInitialSpace(spaces) {
    const stored = window.localStorage.getItem('todo.space');

    if (stored && spaces.includes(stored)) {
      return stored;
    }

    if (spaces.includes(state.defaultSpace)) {
      return state.defaultSpace;
    }

    return spaces[0] || state.defaultSpace;
  }

  async function loadTasks() {
    state.loading = true;
    render();

    try {
      const query = new URLSearchParams({ space: state.space, status: 'all' });
      if (state.search.trim()) {
        query.set('q', state.search.trim());
      }

      const payload = await api(`/api/tasks?${query.toString()}`);
      state.tasks = (payload && payload.tasks) || [];
      state.error = null;
    } catch (error) {
      state.tasks = [];
      state.error = error.message;
      toast(error.message, 'error');
    } finally {
      state.loading = false;
      render();
    }
  }

  // --------------------------------------------------------- mutations

  const STATE_PRIORITY = { ongoing: 0, pending: 1, backlog: 2, completed: 3 };

  // Mirrors the server's default ordering so an optimistically added card lands in the right slot.
  function sortTasks(tasks) {
    return tasks.slice().sort((left, right) => {
      const byState = (STATE_PRIORITY[left.status] ?? 9) - (STATE_PRIORITY[right.status] ?? 9);
      if (byState !== 0) {
        return byState;
      }
      if (left.dueOn !== right.dueOn) {
        if (!left.dueOn) {
          return 1;
        }
        if (!right.dueOn) {
          return -1;
        }
        return left.dueOn < right.dueOn ? -1 : 1;
      }
      return left.id - right.id;
    });
  }

  function replaceTask(task) {
    const index = state.tasks.findIndex((candidate) => candidate.id === task.id);

    if (index >= 0) {
      state.tasks[index] = task;
      state.tasks = sortTasks(state.tasks);
    }

    render();
  }

  async function changeState(task, status) {
    const previous = task;
    const optimistic = Object.assign({}, task, { status });
    state.busy.add(task.id);
    replaceTask(optimistic);

    try {
      const updated = await api(taskPath(task.id), { method: 'PATCH', body: { status } });
      state.busy.delete(task.id);
      replaceTask(updated);
      toast(`"${updated.name}" → ${STATE_LABELS[updated.status] || updated.status}`, 'success');
    } catch (error) {
      state.busy.delete(task.id);
      replaceTask(previous);
      toast(error.message, 'error');
    }
  }

  async function removeTask(task) {
    const previous = state.tasks.slice();
    state.tasks = state.tasks.filter((candidate) => candidate.id !== task.id);
    render();

    try {
      await api(taskPath(task.id), { method: 'DELETE' });
      toast(`Deleted "${task.name}".`, 'success');
    } catch (error) {
      state.tasks = previous;
      render();
      toast(error.message, 'error');
    }
  }

  async function submitEditor(payload) {
    const editing = state.editor;
    if (!editing) {
      return;
    }

    dom.submit.disabled = true;

    try {
      if (editing.mode === 'create') {
        const created = await api('/api/tasks', {
          method: 'POST',
          body: Object.assign({ space: state.space }, payload),
        });
        state.tasks = sortTasks(state.tasks.concat([created]));
        closeDialogs();
        render();
        toast(`Created "${created.name}".`, 'success');
      } else {
        const updated = await api(taskPath(editing.id), { method: 'PATCH', body: payload });
        closeDialogs();
        replaceTask(updated);
        toast(`Saved "${updated.name}".`, 'success');
      }
    } catch (error) {
      showFormError(error);
    } finally {
      dom.submit.disabled = false;
    }
  }

  function showFormError(error) {
    const parts = [error.message];
    if (error.fields) {
      for (const [field, messages] of Object.entries(error.fields)) {
        parts.push(`${field}: ${Array.isArray(messages) ? messages.join(' ') : messages}`);
      }
    }
    dom.formError.textContent = parts.join(' · ');
    dom.formError.hidden = false;
  }

  // ------------------------------------------------------------ dialogs

  function openEditor(task) {
    state.editor = task
      ? { mode: 'edit', id: task.id }
      : { mode: 'create', id: null };

    dom.dialogTitle.textContent = task ? `Edit task #${task.id}` : 'New task';
    dom.form.reset();
    dom.formError.hidden = true;
    dom.name.value = task ? task.name : '';
    dom.description.value = task ? task.description : '';
    dom.due.value = task && task.dueOn ? task.dueOn : '';
    dom.remarks.value = task ? task.remarks : '';
    dom.status.value = task ? task.status : 'pending';

    for (const option of dom.status.options) {
      option.disabled = Boolean(task) && !(ALLOWED_TRANSITIONS[task.status] || []).includes(option.value);
    }

    dom.dialog.showModal();
    dom.name.focus();
  }

  function openDeleteConfirm(task) {
    state.confirm = { id: task.id };
    dom.confirmText.textContent = `"${task.name}" (#${task.id}) will be removed from the sheet. This cannot be undone.`;
    dom.confirmDialog.showModal();
    dom.confirmCancel.focus();
  }

  function openSpaceDialog() {
    dom.spaceForm.reset();
    dom.spaceError.hidden = true;
    dom.spaceDialog.showModal();
    dom.spaceName.focus();
  }

  function closeDialogs() {
    for (const dialog of [dom.dialog, dom.confirmDialog, dom.spaceDialog]) {
      if (dialog && dialog.open) {
        dialog.close();
      }
    }
  }

  async function createSpace() {
    const name = dom.spaceName.value.trim();

    if (!name) {
      dom.spaceError.textContent = 'A space name is required.';
      dom.spaceError.hidden = false;
      return;
    }

    try {
      const created = await api('/api/spaces', { method: 'POST', body: { name } });
      const spaces = await api('/api/spaces');
      state.spaces = (spaces && spaces.spaces) || [];
      state.space = created.name;
      window.localStorage.setItem('todo.space', state.space);
      dom.spaceDialog.close();
      render();
      await loadTasks();
      toast(`Space "${created.name}" created.`, 'success');
    } catch (error) {
      dom.spaceError.textContent = error.message;
      dom.spaceError.hidden = false;
    }
  }

  async function deleteCurrentSpace() {
    if (!state.space || state.space === 'default') {
      return;
    }

    const name = state.space;

    try {
      await api(`/api/spaces/${encodeURIComponent(name)}`, { method: 'DELETE' });
      const spaces = await api('/api/spaces');
      state.spaces = (spaces && spaces.spaces) || [];
      state.space = pickInitialSpace(state.spaces);
      window.localStorage.setItem('todo.space', state.space);
      await loadTasks();
      toast(`Space "${name}" deleted.`, 'success');
    } catch (error) {
      toast(error.message, 'error');
    }
  }

  // ------------------------------------------------------------- render

  function visibleTasks() {
    if (state.filter === 'all') {
      return state.tasks;
    }
    return state.tasks.filter((task) => task.status === state.filter);
  }

  function countFor(filter) {
    return filter === 'all'
      ? state.tasks.length
      : state.tasks.filter((task) => task.status === filter).length;
  }

  function render() {
    const focused = describeFocus();

    renderSpaceSelect();
    renderStats();
    renderChips();
    renderStatusLine();
    renderTasks();
    renderHeaderMeta();

    dom.skeleton.hidden = !state.loading || state.tasks.length > 0;
    restoreFocus(focused);
  }

  function renderHeaderMeta() {
    if (!state.meta.provider) {
      dom.serverMeta.textContent = 'Connecting…';
      return;
    }

    const parts = [state.meta.provider];
    if (state.meta.spreadsheetId) {
      parts.push(`sheet ${state.meta.spreadsheetId}`);
    }
    parts.push(`space ${state.space || 'default'}`);
    dom.serverMeta.textContent = parts.join(' · ');
  }

  function renderSpaceSelect() {
    const spaces = state.spaces.length > 0 ? state.spaces : [state.space || state.defaultSpace];
    const signature = spaces.join('\u0000') + '\u0001' + state.space;

    if (dom.spaceSelect.dataset.signature !== signature) {
      dom.spaceSelect.replaceChildren(
        ...spaces.map((name) => el('option', { value: name, text: name, selected: name === state.space }))
      );
      dom.spaceSelect.dataset.signature = signature;
    }

    dom.spaceSelect.value = state.space;
    dom.deleteSpaceButton.disabled = !state.space || state.space === 'default';
  }

  function renderChips() {
    const nodes = FILTERS.map((filter) => {
      const active = state.filter === filter.id;
      return el('button', {
        type: 'button',
        class: `nav-link${active ? ' active' : ''}`,
        'aria-pressed': String(active),
        dataset: { filter: filter.id },
      }, [
        document.createTextNode(filter.label),
        el('span', { class: 'badge rounded-pill', text: String(countFor(filter.id)) }),
      ]);
    });
    dom.chips.replaceChildren(...nodes);
  }

  function renderStats() {
    const open = state.tasks.filter((task) => task.status !== 'completed');
    const overdue = open.filter((task) => {
      const delta = dayDelta(task.dueOn);
      return delta !== null && delta < 0;
    }).length;

    const tiles = [
      { label: 'Pending', value: countFor('pending'), tone: 'primary', glyph: ICONS.list },
      { label: 'Ongoing', value: countFor('ongoing'), tone: 'warning', glyph: ICONS.play },
      { label: 'Overdue', value: overdue, tone: 'danger', glyph: ICONS.alert },
      { label: 'Completed', value: countFor('completed'), tone: 'success', glyph: ICONS.check },
    ];

    dom.stats.replaceChildren(...tiles.map((tile) => el('div', { class: 'col' }, [
      el('div', { class: 'stat' }, [
        el('span', { class: `stat__icon bg-${tile.tone}-subtle text-${tile.tone}-emphasis` }, [icon(tile.glyph, 18)]),
        el('div', {}, [
          el('span', { class: 'stat__value', text: String(tile.value) }),
          el('span', { class: 'stat__label', text: tile.label }),
        ]),
      ]),
    ])));
  }

  function renderStatusLine() {
    if (state.error) {
      dom.statusLine.textContent = state.error;
      return;
    }

    const shown = visibleTasks().length;
    const total = state.tasks.length;
    const suffix = state.search.trim() ? ` matching "${state.search.trim()}"` : '';
    dom.statusLine.textContent = total === 0
      ? ''
      : `Showing ${shown} of ${total} task${total === 1 ? '' : 's'}${suffix}.`;
  }

  function renderTasks() {
    const tasks = visibleTasks();
    const isEmpty = !state.loading && tasks.length === 0;

    dom.emptyState.hidden = !isEmpty;
    dom.grid.setAttribute('aria-busy', String(state.loading && tasks.length > 0));

    if (isEmpty) {
      const filtered = state.tasks.length > 0 || state.search.trim().length > 0 || state.filter !== 'all';
      dom.emptyTitle.textContent = filtered ? 'Nothing matches' : 'No tasks here';
      dom.emptyText.textContent = filtered
        ? 'Try a different status filter or clear the search.'
        : 'Add the first task for this space to get going.';
      dom.emptyAction.textContent = filtered ? 'Clear filters' : 'New task';
      dom.emptyAction.dataset.mode = filtered ? 'clear' : 'create';
    }

    dom.grid.replaceChildren(...tasks.map(renderCard));
  }

  function renderCard(task) {
    const due = dueDescription(task);
    const overdue = due && due.tone === 'overdue';
    const classes = ['card', 'h-100', 'task-card', `task-card--${overdue ? 'overdue' : task.status}`];

    if (state.busy.has(task.id)) {
      classes.push('task-card--busy');
    }

    const body = [
      el('div', { class: 'd-flex align-items-center justify-content-between gap-2 mb-2' }, [
        el('span', { class: 'id-badge', text: `#${task.id}` }),
        el('span', {
          class: `badge rounded-pill ${STATE_BADGES[task.status] || STATE_BADGES.backlog}`,
          dataset: { state: task.status },
          text: STATE_LABELS[task.status] || task.status,
        }),
      ]),
      el('h3', { class: 'task-card__name mb-2', text: task.name }),
    ];

    if (task.description) {
      body.push(el('p', { class: 'card-text text-body-secondary mb-2', text: task.description }));
    }

    if (due) {
      body.push(el('p', {
        class: `task-card__due mb-2 ${TONE_CLASSES[due.tone] || TONE_CLASSES.normal}`,
        title: due.absolute,
      }, [icon(ICONS.clock, 14), el('span', { text: due.text })]));
    }

    if (task.remarks) {
      const details = el('details', { class: 'task-card__remarks border-top pt-2 mt-2', dataset: { remarks: String(task.id) } }, [
        el('summary', { text: 'Remarks' }),
        el('p', { text: task.remarks }),
      ]);
      details.open = state.remarksOpen.has(task.id);
      details.addEventListener('toggle', () => {
        if (details.open) {
          state.remarksOpen.add(task.id);
        } else {
          state.remarksOpen.delete(task.id);
        }
      });
      body.push(details);
    }

    return el('div', { class: 'col' }, [
      el('article', { class: classes.join(' '), dataset: { task: String(task.id) } }, [
        el('div', { class: 'card-body pb-2' }, body),
        el('div', { class: 'card-footer border-top-0 d-flex flex-wrap align-items-center gap-2' }, renderActions(task)),
      ]),
    ]);
  }

  function renderActions(task) {
    const id = task.id;

    const action = (label, iconPath, variant, handler, dataset, iconOnly) => {
      const button = el('button', {
        type: 'button',
        class: `btn btn-sm btn-${variant}`,
        'aria-label': label,
        title: label,
        dataset: dataset,
      }, [icon(iconPath, 14), iconOnly ? null : el('span', { text: label })]);
      button.addEventListener('click', handler);
      return button;
    };

    const transition = (label, target, iconPath, variant) =>
      action(label, iconPath, variant, () => changeState(task, target), { action: 'state', task: String(id), target });

    const actions = [];

    if (task.status === 'completed') {
      actions.push(transition('Reopen', 'pending', ICONS.undo, 'outline-primary'));
    } else if (task.status === 'backlog') {
      actions.push(transition('Start', 'ongoing', ICONS.play, 'primary'));
      actions.push(transition('Queue', 'pending', ICONS.pause, 'outline-secondary'));
    } else if (task.status === 'pending') {
      actions.push(transition('Start', 'ongoing', ICONS.play, 'primary'));
      actions.push(transition('Complete', 'completed', ICONS.check, 'outline-success'));
    } else {
      actions.push(transition('Complete', 'completed', ICONS.check, 'success'));
      actions.push(transition('Pause', 'pending', ICONS.pause, 'outline-secondary'));
    }

    const edit = action('Edit', ICONS.edit, 'outline-secondary', () => openEditor(task), { action: 'edit', task: String(id) }, true);
    const remove = action('Delete', ICONS.trash, 'outline-danger', () => openDeleteConfirm(task), { action: 'delete', task: String(id) }, true);
    edit.classList.add('ms-auto');

    return actions.concat([edit, remove]);
  }

  // -------------------------------------------------------- focus care

  function describeFocus() {
    const active = document.activeElement;
    if (!active || !active.dataset) {
      return null;
    }

    if (active.dataset.filter) {
      return { chip: active.dataset.filter };
    }

    if (!dom.grid.contains(active)) {
      return null;
    }

    if (active.dataset.action && active.dataset.task) {
      return {
        action: active.dataset.action,
        task: active.dataset.task,
        target: active.dataset.target || '',
      };
    }

    return null;
  }

  function restoreFocus(descriptor) {
    if (!descriptor) {
      return;
    }

    if (descriptor.chip) {
      const chip = dom.chips.querySelector(`[data-filter="${descriptor.chip}"]`);
      if (chip) {
        chip.focus();
      }
      return;
    }

    const card = dom.grid.querySelector(`[data-task="${descriptor.task}"]`);
    if (!card) {
      dom.search.focus();
      return;
    }

    const selector = descriptor.target
      ? `[data-action="${descriptor.action}"][data-target="${descriptor.target}"]`
      : `[data-action="${descriptor.action}"]`;

    const button = card.querySelector(selector) || card.querySelector('[data-action="edit"]');
    if (button) {
      button.focus();
    }
  }

  // -------------------------------------------------------------- wiring

  function cacheDom() {
    dom.serverMeta = document.getElementById('server-meta');
    dom.spaceSelect = document.getElementById('space-select');
    dom.addSpaceButton = document.getElementById('add-space');
    dom.deleteSpaceButton = document.getElementById('delete-space');
    dom.search = document.getElementById('search-input');
    dom.chips = document.getElementById('status-chips');
    dom.stats = document.getElementById('stats');
    dom.newTask = document.getElementById('new-task');
    dom.statusLine = document.getElementById('status-line');
    dom.skeleton = document.getElementById('skeleton');
    dom.grid = document.getElementById('task-grid');
    dom.emptyState = document.getElementById('empty-state');
    dom.emptyTitle = document.getElementById('empty-state-title');
    dom.emptyText = document.getElementById('empty-state-text');
    dom.emptyAction = document.getElementById('empty-state-action');
    dom.dialog = document.getElementById('task-dialog');
    dom.dialogTitle = document.getElementById('task-dialog-title');
    dom.form = document.getElementById('task-form');
    dom.name = document.getElementById('task-name');
    dom.description = document.getElementById('task-description');
    dom.due = document.getElementById('task-due');
    dom.status = document.getElementById('task-status');
    dom.remarks = document.getElementById('task-remarks');
    dom.formError = document.getElementById('task-form-error');
    dom.submit = document.getElementById('task-submit');
    dom.confirmDialog = document.getElementById('confirm-dialog');
    dom.confirmText = document.getElementById('confirm-text');
    dom.confirmAccept = document.getElementById('confirm-accept');
    dom.confirmCancel = document.getElementById('confirm-cancel');
    dom.spaceDialog = document.getElementById('space-dialog');
    dom.spaceForm = document.getElementById('space-form');
    dom.spaceName = document.getElementById('space-name');
    dom.spaceError = document.getElementById('space-error');
    dom.toasts = document.getElementById('toasts');
  }

  function debounce(fn, delay) {
    let timer = null;
    return (...args) => {
      window.clearTimeout(timer);
      timer = window.setTimeout(() => fn(...args), delay);
    };
  }

  const onSearch = debounce(() => {
    loadTasks();
  }, 250);

  function wire() {
    dom.spaceSelect.addEventListener('change', () => {
      state.space = dom.spaceSelect.value;
      window.localStorage.setItem('todo.space', state.space);
      loadTasks();
    });

    dom.search.addEventListener('input', () => {
      state.search = dom.search.value;
      renderStatusLine();
      renderChips();
      onSearch();
    });

    dom.chips.addEventListener('click', (event) => {
      const chip = event.target.closest('[data-filter]');
      if (!chip) {
        return;
      }
      state.filter = chip.dataset.filter;
      render();
    });

    dom.newTask.addEventListener('click', () => openEditor(null));

    dom.emptyAction.addEventListener('click', () => {
      if (dom.emptyAction.dataset.mode === 'clear') {
        state.filter = 'all';
        state.search = '';
        dom.search.value = '';
        loadTasks();
        return;
      }
      openEditor(null);
    });

    dom.form.addEventListener('submit', (event) => {
      event.preventDefault();
      dom.formError.hidden = true;

      const name = dom.name.value.trim();
      if (!name) {
        dom.formError.textContent = 'Name is required.';
        dom.formError.hidden = false;
        dom.name.focus();
        return;
      }

      const payload = { name };
      const description = dom.description.value;
      const remarks = dom.remarks.value;
      const dueOn = dom.due.value;

      if (state.editor && state.editor.mode === 'create') {
        payload.description = description;
        payload.remarks = remarks;
        payload.status = dom.status.value;
        if (dueOn) {
          payload.dueOn = dueOn;
        }
      } else {
        payload.description = description;
        payload.remarks = remarks;
        if (dueOn) {
          payload.dueOn = dueOn;
        } else {
          payload.clearDue = true;
        }

        const current = state.tasks.find((task) => task.id === state.editor.id);
        if (!current || current.status !== dom.status.value) {
          payload.status = dom.status.value;
        }
      }

      submitEditor(payload);
    });

    dom.confirmAccept.addEventListener('click', () => {
      const target = state.confirm && state.tasks.find((task) => task.id === state.confirm.id);
      dom.confirmDialog.close();
      if (target) {
        removeTask(target);
      }
    });

    dom.deleteSpaceButton.addEventListener('click', () => {
      deleteCurrentSpace();
    });

    dom.addSpaceButton.addEventListener('click', () => {
      openSpaceDialog();
    });

    dom.spaceForm.addEventListener('submit', (event) => {
      event.preventDefault();
      createSpace();
    });

    for (const button of document.querySelectorAll('[data-close-dialog]')) {
      button.addEventListener('click', () => {
        const dialog = button.closest('dialog');
        if (dialog) {
          dialog.close();
        }
      });
    }

    document.addEventListener('keydown', (event) => {
      const target = event.target;
      const typing = target instanceof HTMLElement
        && (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.tagName === 'SELECT' || target.isContentEditable);

      if (typing || event.metaKey || event.ctrlKey || event.altKey) {
        return;
      }

      // A modal already traps focus; the shortcuts would fight it.
      if (document.querySelector('dialog[open]')) {
        return;
      }

      if (event.key === '/') {
        event.preventDefault();
        dom.search.focus();
        dom.search.select();
      } else if (event.key === 'n' || event.key === 'N') {
        event.preventDefault();
        openEditor(null);
      }
    });
  }

  document.addEventListener('DOMContentLoaded', () => {
    cacheDom();
    wire();
    bootstrap();
  });
})();
