const video = document.querySelector('#dev-video');
const support = document.querySelector('.coin-support');
const free = document.querySelector('.coin-continue');
const status = document.querySelector('.coin-status');
const reducedMotion = matchMedia('(prefers-reduced-motion: reduce)');
const loopStart = 0.15, loopEnd = 1.05, cryEnd = 7.25, reach = 110;
let mode = 'loop', target = loopStart, animation = 0;
function playLoop() {
  if (mode === 'loop' && !video.paused) return;
  mode = 'loop'; status.textContent = '●  PLAYER CODANDO';
  cancelAnimationFrame(animation);
  if (!Number.isFinite(video.duration)) return;
  if (video.currentTime < loopStart || video.currentTime >= loopEnd) video.currentTime = loopStart;
  if (reducedMotion.matches) { video.pause(); return; }
  video.playbackRate = 1;
  const result = video.play();
  if (result && result.catch) result.catch(() => {});
}
function animateSeek() {
  if (mode === 'loop' || video.seeking) { if (mode !== 'loop') animation = requestAnimationFrame(animateSeek); return; }
  const difference = target - video.currentTime;
  if (Math.abs(difference) > 0.025) {
    video.currentTime += Math.sign(difference) * Math.min(Math.abs(difference), 0.18);
    animation = requestAnimationFrame(animateSeek);
  } else if (mode === 'returning') playLoop();
}
function scrub(progress) {
  document.documentElement.style.setProperty('--storm', String(progress));
  window.dispatchEvent(new CustomEvent('obsidian-weather', { detail: progress }));
  if (!Number.isFinite(video.duration)) return;
  video.pause(); mode = 'scrub';
  target = loopEnd + (cryEnd - loopEnd) * Math.max(0, Math.min(1, progress));
  status.textContent = progress > 0.65 ? '●  PLAYER TRISTINHO' : '●  PLAYER CODANDO';
  cancelAnimationFrame(animation);
  if (reducedMotion.matches) { video.currentTime = progress > .65 ? cryEnd : loopStart; return; }
  animation = requestAnimationFrame(animateSeek);
}
function resumeLoop() {
  document.documentElement.style.setProperty('--storm', '0');
  window.dispatchEvent(new CustomEvent('obsidian-weather', { detail: 0 }));
  if (mode === 'loop') return;
  if (reducedMotion.matches) { playLoop(); return; }
  mode = 'returning'; target = loopStart;
  cancelAnimationFrame(animation); animation = requestAnimationFrame(animateSeek);
}
video.addEventListener('loadedmetadata', playLoop, {once:true});
video.addEventListener('timeupdate', () => { if (mode === 'loop' && video.currentTime >= loopEnd) video.currentTime = loopStart; });
video.addEventListener('ended', () => { if (mode === 'loop') playLoop(); });
document.addEventListener('pointermove', event => {
  if (event.pointerType === 'touch') return;
  const box = free.getBoundingClientRect();
  const dx = Math.max(box.left - event.clientX, 0, event.clientX - box.right);
  const dy = Math.max(box.top - event.clientY, 0, event.clientY - box.bottom);
  const distance = Math.hypot(dx, dy);
  if (distance >= reach) resumeLoop(); else scrub(1 - distance / reach);
});
document.addEventListener('pointerleave', resumeLoop);
support.addEventListener('focus', resumeLoop);
free.addEventListener('focus', () => scrub(1));
free.addEventListener('blur', resumeLoop);
