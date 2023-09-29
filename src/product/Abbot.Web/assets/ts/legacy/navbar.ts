'use strict';

export function navbarOnLoad() {
  // Dropdowns in navbar
  const $dropdowns = document.querySelectorAll('.navbar-item.has-dropdown:not(.is-hoverable)');

  if ($dropdowns.length > 0) {
    $dropdowns.forEach(function ($el) {
      $el.addEventListener('click', function (event) {
        event.stopPropagation();
        $el.classList.toggle('is-active');
      });
    });

    document.addEventListener('click', function () {
      closeDropdowns();
    });
  }

  function closeDropdowns() {
    $dropdowns.forEach(function ($el) {
      $el.classList.remove('is-active');
    });
  }

  // Close dropdowns if ESC pressed
  document.addEventListener('keydown', function (evt) {
    if (evt.key === 'Escape') {
      closeDropdowns();
    }
  });

  // Toggles

  const $burgers = document.querySelectorAll<HTMLElement>('.burger');

  if ($burgers.length > 0) {
    $burgers.forEach(function ($el) {
      $el.addEventListener('click', function () {
        const target = $el.dataset.target;
        const $target = document.getElementById(target);
        $el.classList.toggle('is-active');
        $target.classList.toggle('is-active');
      });
    });
  }
  const $navToggles = document.querySelectorAll('.js-nav-toggle');
  const $navToggleIcons = document.querySelectorAll('.js-nav-toggle-icon');
  const $navContent = document.getElementById('js-nav-content');

  $navToggles.forEach(function ($el) {
    $el.addEventListener('click', function () {
      $navContent.classList.toggle('hidden');

      $navToggleIcons.forEach(function ($iconEl) {
        $iconEl.classList.toggle('hidden');
      });
    });
  });
}