//// ****************************************************************
// * SC Common Utilities
// * Shared utilities for Star Citizen StreamDeck Plugin
// * WebSocket handled by Elgato's sdpi-components.js
//// ****************************************************************

// Region === Shared utilities ===

const INITIAL_STATUS_DELAY = 500;

function escapeHtml(str)
{
    if (str === null || str === undefined)
    {
        return '';
    }
    return String(str)
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll('\'', '&#039;');
}

function debounce(func, wait)
{
    let timeout;
    return function executedFunction(...args)
    {
        const later = () =>
        {
            clearTimeout(timeout);
            func(...args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
    };
}

function onDocumentReady(callback)
{
    if (document.readyState === 'loading')
    {
        document.addEventListener('DOMContentLoaded', callback);
    }
    else
    {
        callback();
    }
}

// Region === DOM helpers ===
function getElement(id)
{
    return document.getElementById(id);
}

function setText(id, text)
{
    const el = getElement(id);
    if (el) el.textContent = text;
}

function getValue(id)
{
    const el = getElement(id);
    return el ? el.value : '';
}

function setValue(id, value)
{
    const el = getElement(id);
    if (el) el.value = value;
}

function addEventListener(id, event, handler)
{
    const el = getElement(id);
    if (el) el.addEventListener(event, handler);
}

function formatTimestamp(date)
{
    if (!date) return 'Never';
    const dateObj = date instanceof Date ? date : new Date(date);
    return dateObj.toLocaleTimeString();
}
