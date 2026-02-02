//// ****************************************************************
// * SC Dropdown
// * Shared dropdown logic for Property Inspector pages
//// ****************************************************************

(function () {
  const root = globalThis;

  function createSvgArrow() {
    const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    svg.setAttribute('class', 'pi-dropdown__arrow');
    svg.setAttribute('fill', 'none');
    svg.setAttribute('height', '20');
    svg.setAttribute('width', '20');
    svg.setAttribute('xmlns', 'http://www.w3.org/2000/svg');

    const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
    path.setAttribute('d', 'm5 7.5 5 5 5-5');
    path.setAttribute('stroke', 'var(--color-primary)');
    path.setAttribute('stroke-linecap', 'round');
    path.setAttribute('stroke-linejoin', 'round');
    path.setAttribute('stroke-width', '2');
    svg.appendChild(path);

    return svg;
  }

  function ensureDropdownMarkup(rootEl, opts = {}) {
    const hasInput = !!rootEl.querySelector('.pi-dropdown__search');
    const hasToggle = !!rootEl.querySelector('.pi-dropdown__toggle');
    const hasMenu = !!rootEl.querySelector('.pi-dropdown__menu');
    if (hasInput && hasToggle && hasMenu) {
      return;
    }

    const placeholder = typeof opts.placeholder === 'string'
      ? opts.placeholder
      : (rootEl.getAttribute('data-placeholder') || '');

    const inputRow = document.createElement('div');
    inputRow.className = 'pi-dropdown__input-row';

    const inputWrapper = document.createElement('div');
    inputWrapper.className = 'pi-dropdown__input-wrapper';

    const input = document.createElement('input');
    input.className = 'pi-dropdown__search';
    input.type = 'text';
    input.placeholder = placeholder;
    inputWrapper.appendChild(input);

    const toggle = document.createElement('div');
    toggle.className = 'pi-dropdown__toggle';
    toggle.appendChild(createSvgArrow());

    inputRow.appendChild(inputWrapper);
    inputRow.appendChild(toggle);

    const menu = document.createElement('div');
    menu.className = 'pi-dropdown__menu';

    rootEl.replaceChildren(inputRow, menu);
  }

  function initDropdown(options = {}) {
    const rootId = options.rootId;
    if (!rootId) {
      return null;
    }

    const rootEl = document.getElementById(rootId);
    if (!rootEl) {
      return null;
    }

    ensureDropdownMarkup(rootEl, {placeholder: options.placeholder});

    const inputEl = rootEl.querySelector('.pi-dropdown__search');
    const toggleEl = rootEl.querySelector('.pi-dropdown__toggle');
    const arrowEl = rootEl.querySelector('.pi-dropdown__arrow');
    const menuEl = rootEl.querySelector('.pi-dropdown__menu');
    const inputWrapperEl = rootEl.querySelector('.pi-dropdown__input-wrapper');
    if (!inputEl || !toggleEl || !menuEl) {
      return null;
    }

    const searchEnabled = options.searchEnabled !== false;
    const maxResults = typeof options.maxResults === 'number' ? options.maxResults : 50;
    const getText = typeof options.getText === 'function' ? options.getText : (item) => String(item?.text ?? '');
    const getValue = typeof options.getValue === 'function' ? options.getValue : (item) => String(item?.value ?? '');
    const getGroup = typeof options.getGroup === 'function' ? options.getGroup : null;
    const isDisabled = typeof options.isDisabled === 'function' ? options.isDisabled : (item) => !!item?.disabled;
    const onSelect = typeof options.onSelect === 'function' ? options.onSelect : null;
    const emptyText = typeof options.emptyText === 'string' ? options.emptyText : 'No items found';
    const displaySelectedInInput = options.displaySelectedInInput !== undefined
      ? !!options.displaySelectedInInput
      : !searchEnabled;

    const minLoadingMs = typeof options.minLoadingMs === 'number' ? options.minLoadingMs : 500;
    const successFlashMs = typeof options.successFlashMs === 'number' ? options.successFlashMs : 220;
    const successText = typeof options.successText === 'string' ? options.successText : '';

    let items = [];
    let selectedValue = '';
    let isSelecting = false;
    let isBusy = false;
    let loadingStartedAt = 0;
    let loadingToken = 0;
    let pendingTimer = null;

    // Inline loading overlay (spinner + "Loading" text + animated dots)
    let loadingEl = null;
    let loadingLabelEl = null;
    let loadingDotsEl = null;

    if (inputWrapperEl) {
      loadingEl = document.createElement('div');
      loadingEl.className = 'pi-dropdown__loading';
      loadingEl.setAttribute('aria-hidden', 'true');

      const spinner = document.createElement('span');
      spinner.className = 'pi-spinner';
      spinner.setAttribute('aria-hidden', 'true');

      loadingLabelEl = document.createElement('span');
      loadingLabelEl.className = 'pi-dropdown__loading-label';

      loadingDotsEl = document.createElement('span');
      loadingDotsEl.className = 'pi-dropdown__loading-dots';

      for (let i = 0; i < 3; i += 1) {
        const dot = document.createElement('span');
        dot.className = 'pi-dropdown__loading-dot';
        dot.textContent = '.';
        loadingDotsEl.appendChild(dot);
      }

      // Keep label aligned with the input text (spinner on the right avoids a left "snap" when loading ends).
      loadingEl.appendChild(loadingLabelEl);
      loadingEl.appendChild(loadingDotsEl);
      loadingEl.appendChild(spinner);
      inputWrapperEl.appendChild(loadingEl);
    }

    if (!searchEnabled) {
      inputEl.readOnly = true;
      inputEl.setAttribute('readonly', '');
    }

    function isOpen() {
      return rootEl.classList.contains('pi-dropdown--open');
    }

    function setArrowOpen(open) {
      if (!arrowEl) {
        return;
      }
      if (open) {
        arrowEl.classList.add('pi-dropdown__arrow--open');
      } else {
        arrowEl.classList.remove('pi-dropdown__arrow--open');
      }
    }

    function render(list) {
      menuEl.textContent = '';

      if (!Array.isArray(list) || list.length === 0) {
        const emptyEl = document.createElement('div');
        emptyEl.className = 'pi-dropdown__empty-state';
        emptyEl.textContent = emptyText;
        menuEl.appendChild(emptyEl);
        return;
      }

      if (getGroup) {
        const grouped = new Map();
        for (const item of list) {
          const groupName = String(getGroup(item) ?? '');
          if (!grouped.has(groupName)) {
            grouped.set(groupName, []);
          }
          grouped.get(groupName).push(item);
        }

        for (const [groupName, groupItems] of grouped.entries()) {
          if (groupName) {
            const header = document.createElement('div');
            header.className = 'pi-dropdown__group-header';
            header.textContent = groupName;
            menuEl.appendChild(header);
          }

          for (const item of groupItems) {
            menuEl.appendChild(renderOption(item));
          }
        }
      } else {
        for (const item of list) {
          menuEl.appendChild(renderOption(item));
        }
      }
    }

    function renderOption(item) {
      const optionEl = document.createElement('div');
      optionEl.className = 'pi-dropdown__option';

      const isUnbound = !!item?.unbound;

      const disabled = isDisabled(item);
      if (disabled) {
        optionEl.classList.add('disabled');
      }

      const nameEl = document.createElement('span');
      nameEl.className = 'pi-dropdown__option-label';
      nameEl.textContent = getText(item);
      optionEl.appendChild(nameEl);

      if (isUnbound) {
        const badgeEl = document.createElement('span');
        badgeEl.className = 'pi-dropdown__option-badge pi-dropdown__option-badge--warn';
        badgeEl.textContent = '!';
        badgeEl.title = 'Unbound';
        optionEl.appendChild(badgeEl);
      }

      optionEl.addEventListener('mousedown', () => {
        isSelecting = true;
      });

      optionEl.addEventListener('click', () => {
        if (disabled) {
          return;
        }
        selectItem(item);
      });

      return optionEl;
    }

    function filter(searchText) {
      const q = (searchText || '').toLowerCase().trim();
      let filtered = items.filter((item) => getText(item).toLowerCase().includes(q));
      if (filtered.length > maxResults) {
        filtered = filtered.slice(0, maxResults);
      }
      return filtered;
    }

    function open() {
      if (isBusy) {
        return;
      }
      rootEl.classList.add('pi-dropdown--open');
      setArrowOpen(true);

      if (searchEnabled) {
        const q = (inputEl.value || '').trim();
        render(q ? filter(q) : items);
      } else {
        render(items);
      }
    }

    function close() {
      rootEl.classList.remove('pi-dropdown--open');
      setArrowOpen(false);
    }

    function toggle() {
      if (isBusy) {
        return;
      }
      if (isOpen()) {
        close();
      } else {
        open();
      }
    }

    function setItems(newItems) {
      items = Array.isArray(newItems) ? newItems : [];
      if (isOpen()) {
        open();
      }
    }

    function setSelectedValue(value, opts = {}) {
      selectedValue = typeof value === 'string' ? value : '';
      if (displaySelectedInInput) {
        const selectedItem = items.find((it) => getValue(it) === selectedValue);
        inputEl.value = selectedItem ? getText(selectedItem) : '';
      }

      if (opts.rerender && isOpen()) {
        open();
      }
    }

    function selectItem(item) {
      if (isBusy) {
        return;
      }
      selectedValue = getValue(item);

      if (displaySelectedInInput) {
        inputEl.value = getText(item);
      }

      if (searchEnabled) {
        inputEl.value = '';
      }

      close();

      if (onSelect) {
        onSelect(item);
      }

      setTimeout(() => {
        isSelecting = false;
      }, 200);
    }

    function handleInput(e) {
      if (!searchEnabled) {
        return;
      }

      if (isBusy) {
        return;
      }

      const q = e?.target?.value ?? '';
      render(filter(q));
      if (!isOpen() && String(q).trim()) {
        open();
      }
    }

    function handleBlur() {
      if (!searchEnabled) {
        return;
      }

      if (isSelecting) {
        return;
      }

      if (isOpen()) {
        close();
      }

      inputEl.value = '';
    }

    function handleInputClick(e) {
      if (searchEnabled) {
        return;
      }

      if (isBusy) {
        return;
      }
      e?.stopPropagation?.();
      toggle();
    }

    const initialReadOnly = inputEl.readOnly;
    const initialReadonlyAttr = inputEl.hasAttribute('readonly');

    function setLoading(loading, text = 'Loading') {
      const next = !!loading;
      if (next === isBusy) {
        if (isBusy && loadingLabelEl) {
          loadingLabelEl.textContent = String(text || 'Loading');
        }
        return;
      }

      // Cancel any pending state transitions.
      loadingToken += 1;
      if (pendingTimer) {
        clearTimeout(pendingTimer);
        pendingTimer = null;
      }

      if (next) {
        isBusy = true;
        loadingStartedAt = Date.now();

        close();
        rootEl.classList.remove('pi-dropdown--success');
        rootEl.classList.add('pi-dropdown--loading');
        inputEl.readOnly = true;
        inputEl.setAttribute('readonly', '');
        inputEl.blur?.();
        toggleEl.setAttribute('aria-disabled', 'true');

        if (loadingLabelEl) {
          loadingLabelEl.textContent = String(text || 'Loading');
        }
        return;
      }

      // Transition from loading -> success -> idle.
      const token = loadingToken;
      const elapsed = Date.now() - loadingStartedAt;
      const remaining = Math.max(0, minLoadingMs - elapsed);

      pendingTimer = setTimeout(() => {
        if (token !== loadingToken) {
          return;
        }

        rootEl.classList.remove('pi-dropdown--loading');
        rootEl.classList.add('pi-dropdown--success');

        if (loadingLabelEl && typeof successText === 'string' && successText.trim().length > 0) {
          loadingLabelEl.textContent = successText;
        }

        pendingTimer = setTimeout(() => {
          if (token !== loadingToken) {
            return;
          }

          rootEl.classList.remove('pi-dropdown--success');
          toggleEl.removeAttribute('aria-disabled');

          inputEl.readOnly = initialReadOnly;
          if (initialReadonlyAttr) {
            inputEl.setAttribute('readonly', '');
          } else {
            inputEl.removeAttribute('readonly');
          }

          isBusy = false;
          pendingTimer = null;
        }, successFlashMs);
      }, remaining);
    }

    // Wire listeners
    if (searchEnabled) {
      const debouncer = root.SCPI?.util?.debounce || root.debounce;
      const debounced = typeof debouncer === 'function' ? debouncer(handleInput, 150) : handleInput;
      inputEl.addEventListener('input', debounced);
      inputEl.addEventListener('blur', handleBlur);
    } else {
      inputEl.addEventListener('click', handleInputClick);
    }

    toggleEl.addEventListener('click', (e) => {
      e.stopPropagation();
      toggle();
    });

    document.addEventListener('click', (event) => {
      if (!rootEl.contains(event.target)) {
        if (isOpen()) {
          close();
        }
        if (searchEnabled) {
          inputEl.value = '';
        }
      }
    }, true);

    return {
      setItems,
      setSelectedValue,
      setLoading
    };
  }

  const SCPI = root.SCPI = root.SCPI || {};
  SCPI.ui = SCPI.ui || {};
  SCPI.ui.dropdown = {
    initDropdown
  };

  // Back-compat
  root.SCDropdown = root.SCDropdown || SCPI.ui.dropdown;
})();
