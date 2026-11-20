/*
 * Notes view: list + markdown viewer + editor with live preview. Shares api/el/toast with app.js.
 * Markdown is rendered with marked and always sanitized with DOMPurify: the sheet is shared, so a
 * note can contain anything.
 */
(() => {
  'use strict';

  const { api, el, toast } = window.todoApp;

  const state = {
    notes: [],
    loaded: false,
    loading: false,
    error: null,
    search: '',
    selectedId: null,
    editor: null, // { mode: 'create' | 'edit', id }
  };

  const dom = {};

  // ----------------------------------------------------------- markdown

  if (window.DOMPurify) {
    window.DOMPurify.addHook('afterSanitizeAttributes', (node) => {
      // Only marked's read-only task-list checkboxes survive; any other raw <input> is dropped.
      if (node.tagName === 'INPUT') {
        if (node.getAttribute('type') === 'checkbox') {
          node.setAttribute('disabled', '');
        } else {
          node.remove();
        }
      }
      if (node.tagName === 'A' && node.hasAttribute('href')) {
        node.setAttribute('target', '_blank');
        node.setAttribute('rel', 'noopener noreferrer');
      }
    });
  }

  function renderMarkdown(source) {
    const html = window.marked.parse(source || '', { gfm: true, async: false });
    return window.DOMPurify.sanitize(html, { FORBID_TAGS: ['style', 'form', 'textarea', 'select', 'button'] });
  }

  function setMarkdown(node, source) {
    node.innerHTML = source && source.trim() ? renderMarkdown(source) : '';
    if (!node.innerHTML) {
      node.append(el('p', { class: 'text-body-secondary fst-italic mb-0', text: 'Nothing to preview.' }));
    }
  }

  // ---------------------------------------------------------- formatting

  function formatDate(iso) {
    return new Date(iso).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' });
  }

  function snippet(content) {
    return content.replace(/[#>*_`~\[\]()!|-]+/g, ' ').replace(/\s+/g, ' ').trim().slice(0, 90);
  }

  // -------------------------------------------------------------- routing

  function route() {
    const match = /^#\/notes(?:\/(\d+))?$/.exec(window.location.hash);
    const active = Boolean(match);

    document.body.dataset.view = active ? 'notes' : 'tasks';
    document.getElementById('tasks-view').hidden = active;
    dom.view.hidden = !active;

    for (const link of document.querySelectorAll('#view-nav [data-view]')) {
      const current = link.dataset.view === (active ? 'notes' : 'tasks');
      link.classList.toggle('active', current);
      if (current) {
        link.setAttribute('aria-current', 'page');
      } else {
        link.removeAttribute('aria-current');
      }
    }

    if (!active) {
      return;
    }

    const wanted = match[1] ? Number(match[1]) : null;

    if (state.editor && wanted !== state.selectedId && !(state.editor.mode === 'create' && wanted === null)) {
      // Never drop an edit silently; put the hash back instead.
      toast('Save or cancel your edit first.', 'error');
      window.location.hash = state.selectedId ? `#/notes/${state.selectedId}` : '#/notes';
      return;
    }

    state.selectedId = wanted;
    render();

    if (!state.loaded && !state.loading) {
      load();
    }
  }

  function go(id) {
    const hash = id ? `#/notes/${id}` : '#/notes';
    if (window.location.hash === hash) {
      route();
    } else {
      window.location.hash = hash;
    }
  }

  // ------------------------------------------------------------- data

  async function load() {
    state.loading = true;
    render();

    try {
      const payload = await api('/api/notes');
      state.notes = (payload && payload.notes) || [];
      state.error = null;
      state.loaded = true;
    } catch (error) {
      state.error = error.message;
      toast(error.message, 'error');
    } finally {
      state.loading = false;
      render();
    }
  }

  function selected() {
    return state.notes.find((note) => note.id === state.selectedId) || null;
  }

  function upsert(note) {
    const index = state.notes.findIndex((candidate) => candidate.id === note.id);
    if (index >= 0) {
      state.notes[index] = note;
    } else {
      state.notes.unshift(note);
    }
    state.notes.sort((left, right) => right.id - left.id);
  }

  async function save() {
    const editor = state.editor;
    const title = dom.title.value.trim();

    if (!editor) {
      return;
    }
    if (!title) {
      showError('Title is required.');
      dom.title.focus();
      return;
    }

    dom.save.disabled = true;
    dom.error.hidden = true;

    try {
      const body = { title, content: dom.content.value };
      const note = editor.mode === 'create'
        ? await api('/api/notes', { method: 'POST', body })
        : await api(`/api/notes/${editor.id}`, { method: 'PATCH', body });

      upsert(note);
      state.editor = null;
      toast(`Saved "${note.title}".`, 'success');
      go(note.id);
    } catch (error) {
      showError(error);
    } finally {
      dom.save.disabled = false;
    }
  }

  function showError(error) {
    const parts = [error.message || String(error)];
    if (error.fields) {
      for (const [field, messages] of Object.entries(error.fields)) {
        parts.push(`${field}: ${Array.isArray(messages) ? messages.join(' ') : messages}`);
      }
    }
    dom.error.textContent = parts.join(' · ');
    dom.error.hidden = false;
  }

  async function remove(note) {
    try {
      await api(`/api/notes/${note.id}`, { method: 'DELETE' });
      state.notes = state.notes.filter((candidate) => candidate.id !== note.id);
      toast(`Deleted "${note.title}".`, 'success');
      go(null);
    } catch (error) {
      toast(error.message, 'error');
    }
  }

  // ------------------------------------------------------------ editor

  function startEditor(note) {
    state.editor = note ? { mode: 'edit', id: note.id } : { mode: 'create', id: null };
    dom.title.value = note ? note.title : '';
    dom.content.value = note ? note.content : '';
    dom.error.hidden = true;

    if (!note) {
      state.selectedId = null;
      if (window.location.hash !== '#/notes') {
        window.location.hash = '#/notes';
      }
    }

    render();
    dom.title.focus();
  }

  function cancelEditor() {
    const wasCreate = state.editor && state.editor.mode === 'create';
    state.editor = null;
    if (wasCreate) {
      go(null);
    } else {
      render();
    }
  }

  // ------------------------------------------------------------ render

  function visibleNotes() {
    const term = state.search.trim().toLowerCase();
    return term
      ? state.notes.filter((note) => note.title.toLowerCase().includes(term) || note.content.toLowerCase().includes(term))
      : state.notes;
  }

  function render() {
    if (dom.view.hidden) {
      return;
    }

    renderList();
    renderPane();
  }

  function renderList() {
    const notes = visibleNotes();
    dom.meta.textContent = state.error
      || (state.loaded ? `${state.notes.length} note${state.notes.length === 1 ? '' : 's'} · stored on the “_notes” tab` : 'Loading…');

    if (notes.length === 0) {
      const text = state.loading ? 'Loading…' : state.search.trim() ? 'No notes match.' : 'No notes yet.';
      dom.list.replaceChildren(el('div', { class: 'list-group-item text-body-secondary', text }));
      return;
    }

    dom.list.replaceChildren(...notes.map((note) => {
      const active = note.id === state.selectedId;
      return el('a', {
        href: `#/notes/${note.id}`,
        class: `list-group-item list-group-item-action${active ? ' active' : ''}`,
        'aria-current': active ? 'true' : null,
        dataset: { note: String(note.id) },
      }, [
        el('div', { class: 'd-flex justify-content-between gap-2' }, [
          el('span', { class: 'fw-semibold text-truncate', text: note.title }),
          el('small', { class: 'text-nowrap opacity-75', text: new Date(note.createdAt).toLocaleDateString() }),
        ]),
        el('div', { class: 'small text-truncate opacity-75', text: snippet(note.content) || 'Empty note' }),
      ]);
    }));
  }

  function renderPane() {
    const note = selected();
    const editing = Boolean(state.editor);

    dom.form.hidden = !editing;
    dom.article.hidden = editing || !note;
    dom.empty.hidden = editing || Boolean(note);

    if (editing) {
      setMarkdown(dom.preview, dom.content.value);
      return;
    }

    if (note) {
      dom.viewTitle.textContent = note.title;
      dom.viewMeta.textContent = `#${note.id} · created ${formatDate(note.createdAt)}`;
      setMarkdown(dom.viewBody, note.content);
      return;
    }

    const missing = state.loaded && state.selectedId !== null;
    dom.emptyTitle.textContent = missing ? 'Note not found' : 'No note selected';
    dom.emptyText.textContent = missing
      ? `There is no note #${state.selectedId}.`
      : 'Pick a note from the list, or create a new one.';
  }

  // ------------------------------------------------------------ wiring

  function debounce(fn, delay) {
    let timer = null;
    return () => {
      window.clearTimeout(timer);
      timer = window.setTimeout(fn, delay);
    };
  }

  function wire() {
    const refreshPreview = debounce(() => setMarkdown(dom.preview, dom.content.value), 150);

    window.addEventListener('hashchange', route);

    dom.search.addEventListener('input', () => {
      state.search = dom.search.value;
      renderList();
    });

    dom.newNote.addEventListener('click', () => startEditor(null));
    dom.edit.addEventListener('click', () => startEditor(selected()));
    dom.cancel.addEventListener('click', cancelEditor);
    dom.content.addEventListener('input', refreshPreview);

    dom.form.addEventListener('submit', (event) => {
      event.preventDefault();
      save();
    });

    dom.form.addEventListener('keydown', (event) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's') {
        event.preventDefault();
        save();
      } else if (event.key === 'Escape' && !document.querySelector('dialog[open]')) {
        cancelEditor();
      }
    });

    dom.delete.addEventListener('click', () => {
      const note = selected();
      if (note) {
        dom.confirmText.textContent = `"${note.title}" (#${note.id}) will be removed from the sheet. This cannot be undone.`;
        dom.confirm.showModal();
        dom.confirmCancel.focus();
      }
    });

    dom.confirmAccept.addEventListener('click', () => {
      const note = selected();
      dom.confirm.close();
      if (note) {
        remove(note);
      }
    });
  }

  function cacheDom() {
    const byId = (id) => document.getElementById(id);
    dom.view = byId('notes-view');
    dom.meta = byId('notes-meta');
    dom.newNote = byId('new-note');
    dom.search = byId('note-search');
    dom.list = byId('note-list');
    dom.article = byId('note-view');
    dom.viewTitle = byId('note-view-title');
    dom.viewMeta = byId('note-view-meta');
    dom.viewBody = byId('note-view-body');
    dom.edit = byId('note-edit');
    dom.delete = byId('note-delete');
    dom.form = byId('note-form');
    dom.title = byId('note-title');
    dom.content = byId('note-content');
    dom.preview = byId('note-preview');
    dom.error = byId('note-form-error');
    dom.save = byId('note-save');
    dom.cancel = byId('note-cancel');
    dom.empty = byId('note-empty');
    dom.emptyTitle = byId('note-empty-title');
    dom.emptyText = byId('note-empty-text');
    dom.confirm = byId('note-confirm-dialog');
    dom.confirmText = byId('note-confirm-text');
    dom.confirmAccept = byId('note-confirm-accept');
    dom.confirmCancel = byId('note-confirm-cancel');
  }

  document.addEventListener('DOMContentLoaded', () => {
    cacheDom();
    wire();
    route();
  });
})();
