/* Local-only presentation: no API calls, analytics, or app dependencies. */
(() => {
  'use strict';
  const reduced = matchMedia('(prefers-reduced-motion: reduce)');
  const videos = [...document.querySelectorAll('video')];
  const visibleVideos = new Set();
  const videoObserver = new IntersectionObserver(entries => {
    for (const entry of entries) {
      const video = entry.target;
      if (entry.isIntersecting) visibleVideos.add(video); else visibleVideos.delete(video);
    }
    updateVideos();
  }, {threshold: .08});
  function updateVideos() {
    for (const video of videos) {
      video.autoplay = !reduced.matches;
      if (!reduced.matches && !document.hidden && visibleVideos.has(video)) video.play().catch(() => {});
      else video.pause();
    }
  }
  for (const video of videos) videoObserver.observe(video);

  const carousel = document.querySelector('.template-carousel');
  const cards = [...carousel.querySelectorAll('[data-template]')];
  const dots = [...carousel.querySelectorAll('[data-template-select]')];
  let current = 0, timer, visible = false, hovered = false, focused = false;
  function select(index) {
    current = (index + cards.length) % cards.length;
    cards.forEach((card, i) => {
      const role = i === current ? 'is-active' : i === (current + 1) % cards.length ? 'is-right' : 'is-left';
      card.classList.remove('is-active', 'is-left', 'is-right');
      card.classList.add(role);
      card.setAttribute('aria-hidden', String(!reduced.matches && i !== current));
    });
    dots.forEach((dot, i) => dot.setAttribute('aria-pressed', String(i === current)));
  }
  function schedule() {
    clearTimeout(timer);
    if (!reduced.matches && !document.hidden && visible && !hovered && !focused) {
      timer = setTimeout(() => {select(current + 1); schedule();}, 6000);
    }
  }
  const carouselObserver = new IntersectionObserver(([entry]) => {
    visible = entry.isIntersecting;
    schedule();
  }, {threshold: .2});
  carouselObserver.observe(carousel);
  carousel.addEventListener('pointerenter', event => {if (event.pointerType === 'mouse') {hovered = true; schedule();}});
  carousel.addEventListener('pointerleave', () => {hovered = false; schedule();});
  carousel.addEventListener('focusin', event => {focused = event.target.matches(':focus-visible'); schedule();});
  carousel.addEventListener('focusout', event => {focused = carousel.contains(event.relatedTarget) && event.relatedTarget.matches(':focus-visible'); schedule();});
  carousel.addEventListener('keydown', event => {
    if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') return;
    event.preventDefault();select(current + (event.key === 'ArrowRight' ? 1 : -1));schedule();
  });
  dots.forEach(dot => dot.addEventListener('click', () => {select(Number(dot.dataset.templateSelect));schedule();}));
  document.addEventListener('visibilitychange', () => {updateVideos(); schedule();});
  reduced.addEventListener('change', () => {updateVideos(); select(current); schedule();});
  select(0);
})();
