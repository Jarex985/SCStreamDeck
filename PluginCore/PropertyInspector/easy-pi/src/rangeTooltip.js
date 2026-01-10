// ****************************************************************
// * EasyPI v2.0
// * Author: BarRaider
// *
// * rangeTooltip.js adds a tooltip showing the value of a range slider.
// * Requires rangeTooltip.css to be referenced in the HTML file.
// *
// * Project page: https://github.com/BarRaider/streamdeck-easypi-v2
// * Support: http://discord.barraider.com
// ****************************************************************

var tooltip = document.querySelector('.sdpi-info-label');
var tw;

document.addEventListener("DOMContentLoaded", function () {
    // Handler when the DOM is fully loaded
    setRangeTooltips();
});

function calcRangeLabel(elem) {
    const value = elem.value;
    const percent = (elem.value - elem.min) / (elem.max - elem.min);
    let tooltipValue = value;
    let outputType = elem.dataset.suffix;
    if (outputType && outputType == '%') {
        tooltipValue = Math.round(100 * percent);
    }

    return tooltipValue + outputType;
}

function setElementLabel(elem, str) {
    // Try to set this for the rangeLabel class, if it exists
    let label = elem.querySelector('.rangeLabel');
    if (label) {
        label.innerHTML = str;
    }
    else {
        console.log('setElementLabel ERROR! No .rangeLabel found', elem);
    }
}

function setRangeTooltips() {
    console.log("Loading setRangeTooltips");

    if (!tooltip) {
        tooltip = document.querySelector('.sdpi-info-label');
    }

    if (!tw) {
        tw = tooltip.getBoundingClientRect().width;
    }

    const rangeToolTips = document.querySelectorAll('div[type=range].sdShowTooltip');
    rangeToolTips.forEach(elem => {
        let rangeSelector = elem.querySelector('input[type=range]');
        let fn = () => {
            const rangeRect = rangeSelector.getBoundingClientRect();
            const w = rangeRect.width - tw / 2;
            const labelStr = calcRangeLabel(rangeSelector);
            // Set the tooltip
            if (tooltip.classList.contains('hidden')) {
                tooltip.style.top = '-1000px';
            } else {
                const percent = (rangeSelector.value - rangeSelector.min) / (rangeSelector.max - rangeSelector.min);
                tooltip.style.left = (rangeRect.left + Math.round(w * percent) - tw / 4) + 'px';
                tooltip.textContent = labelStr;
                tooltip.style.top = (rangeRect.top - 32) + 'px';
            }

            setElementLabel(elem, labelStr)
        };

        rangeSelector.addEventListener(
            'mouseenter',
            function () {
                tooltip.classList.remove('hidden');
                tooltip.classList.add('shown');
                fn();
            },
            false
        );

        rangeSelector.addEventListener(
            'mouseout',
            function () {
                tooltip.classList.remove('shown');
                tooltip.classList.add('hidden');
                fn();
            },
            false
        );
        rangeSelector.addEventListener('input', fn, false);

        rangeSelector.addEventListener("change", fn, false);

        document.addEventListener(
            'settingsUpdated',
            function () {
                console.log('rangeTooltip settingsUpdated called');
                window.setTimeout(function () {
                    let str = calcRangeLabel(rangeSelector);
                    setElementLabel(elem, str);
                }, 500);
            },
            false
        );

        document.addEventListener(
            'websocketCreate',
            function () {
                console.log('rangeTooltip websocketCreate called');
                window.setTimeout(function () {
                    let str = calcRangeLabel(rangeSelector);
                    setElementLabel(elem, str);
                }, 500);
            },
            false
        );
    });

}