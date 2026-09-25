<script setup lang="ts">
import { computed, onUnmounted, ref } from 'vue'
import UnitSprite from '@/components/UnitSprite.vue'
import { spriteForUnit } from '@/sprites/spriteCatalog'
import { attackDuration, poseAtElapsed, type SpriteFacing, type SpritePose } from '@/sprites/spriteAnimation'
import { knightWalkCandidates, walkPhaseAtElapsed, type WalkPhase } from '@/sprites/knightWalkPreview'

const sprite = spriteForUnit('knight')!
const facing = ref<SpriteFacing>('right')
const pose = ref<SpritePose>('idle')
const isPlaying = ref(false)
const reducedMotion = typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches
const loop = ref(!reducedMotion)
const walkCandidate = ref(0)
const walkPhase = ref<WalkPhase>('leftPassing')
const isWalking = ref(false)
const activeWalkCandidate = computed(() => knightWalkCandidates[walkCandidate.value]!)
const activeWalkFrame = computed(() => activeWalkCandidate.value.frames[walkPhase.value])
const phaseLabels: Record<SpritePose, string> = {
  idle: 'Ready',
  windUp: 'Wind-up',
  strike: 'Strike',
  recovery: 'Recovery',
}
const phaseLabel = computed(() => phaseLabels[pose.value])
let animationFrame: number | undefined
let startedAt = 0
let walkAnimationFrame: number | undefined
let walkStartedAt = 0

function stop(): void {
  if (animationFrame !== undefined) {
    cancelAnimationFrame(animationFrame)
    animationFrame = undefined
  }
  isPlaying.value = false
}

function tick(now: number): void {
  const elapsed = now - startedAt
  pose.value = poseAtElapsed(sprite, elapsed)
  if (elapsed >= attackDuration(sprite) + 700) {
    if (!loop.value) {
      stop()
      return
    }
    startedAt = now
  }
  animationFrame = requestAnimationFrame(tick)
}

function attack(): void {
  stop()
  startedAt = performance.now()
  pose.value = 'windUp'
  isPlaying.value = true
  animationFrame = requestAnimationFrame(tick)
}

function showPose(next: SpritePose): void {
  stop()
  pose.value = next
}

function stopWalk(): void {
  if (walkAnimationFrame !== undefined) {
    cancelAnimationFrame(walkAnimationFrame)
    walkAnimationFrame = undefined
  }
  isWalking.value = false
}

function walkTick(now: number): void {
  walkPhase.value = walkPhaseAtElapsed(now - walkStartedAt)
  walkAnimationFrame = requestAnimationFrame(walkTick)
}

function startWalk(): void {
  stopWalk()
  walkStartedAt = performance.now()
  isWalking.value = true
  walkAnimationFrame = requestAnimationFrame(walkTick)
}

function selectWalkCandidate(index: number): void {
  walkCandidate.value = index
  walkPhase.value = 'leftPassing'
  if (isWalking.value) {
    startWalk()
  }
}

onUnmounted(() => {
  stop()
  stopWalk()
})
</script>

<template>
  <main class="sprite-preview">
    <a class="back" href="/">Back to battle</a>
    <p class="eyebrow">AUTO CHESS · PIXEL ART</p>
    <h1>The knight</h1>
    <p class="intro">Steel armor, a plain metal shield, and a short sword. Try the attack in either direction.</p>

    <div class="stage">
      <svg viewBox="-90 -100 180 125" role="img" :aria-label="`Knight facing ${facing}, ${phaseLabel}`">
        <ellipse cx="0" cy="1" rx="28" ry="7" fill="#101823" />
        <UnitSprite :sprite="sprite" :pose="pose" :facing="facing" />
      </svg>
    </div>

    <div class="controls">
      <button type="button" :aria-pressed="facing === 'left'" @click="facing = 'left'">Face left</button>
      <button type="button" :aria-pressed="facing === 'right'" @click="facing = 'right'">Face right</button>
      <button type="button" class="attack" @click="attack">{{ isPlaying ? 'Restart attack' : 'Attack' }}</button>
      <button type="button" @click="showPose('idle')">Idle</button>
      <label><input v-model="loop" type="checkbox" /> Repeat</label>
    </div>

    <p class="phase">Pose: {{ phaseLabel }}</p>
    <div class="poses" aria-label="Choose a pose">
      <button v-for="(name, key) in phaseLabels" :key="key" type="button" :aria-pressed="pose === key" @click="showPose(key)">
        {{ name }}
      </button>
    </div>

    <section class="size-check">
      <h2>Board size</h2>
      <svg width="180" height="125" viewBox="-90 -100 180 125" role="img" aria-label="Knight at board size">
        <UnitSprite :sprite="sprite" :pose="pose" :facing="facing" />
      </svg>
    </section>

    <section class="walk-review">
      <h2>Walk-loop review</h2>
      <p class="intro">The selected metal knight walk loop. Battle movement is integrated separately.</p>
      <div class="stage">
        <svg viewBox="-90 -100 180 125" role="img" :aria-label="`${activeWalkCandidate.name}, ${walkPhase}`">
          <ellipse cx="0" cy="1" rx="28" ry="7" fill="#101823" />
          <g :transform="`scale(${facing === 'left' ? -1 : 1}, 1)`" aria-hidden="true">
            <svg
              :x="-activeWalkFrame.anchorX * activeWalkCandidate.scale"
              :y="-activeWalkFrame.anchorY * activeWalkCandidate.scale"
              :width="activeWalkFrame.width * activeWalkCandidate.scale"
              :height="activeWalkFrame.height * activeWalkCandidate.scale"
              :viewBox="`${activeWalkFrame.x} ${activeWalkFrame.y} ${activeWalkFrame.width} ${activeWalkFrame.height}`"
              overflow="hidden"
            >
              <image :href="activeWalkCandidate.sheets[walkPhase]" :width="activeWalkCandidate.width" :height="activeWalkCandidate.height" style="image-rendering: pixelated" />
            </svg>
          </g>
        </svg>
      </div>
      <div class="controls">
        <button
          v-for="(candidate, index) in knightWalkCandidates"
          :key="candidate.name"
          type="button"
          :aria-pressed="walkCandidate === index"
          @click="selectWalkCandidate(index)"
        >
          {{ candidate.name }}
        </button>
        <button type="button" class="attack" @click="isWalking ? stopWalk() : startWalk()">
          {{ isWalking ? 'Stop walk' : 'Play walk' }}
        </button>
      </div>
      <p class="phase">Phase: {{ walkPhase }}</p>
    </section>
  </main>
</template>

<style scoped>
.sprite-preview {
  box-sizing: border-box;
  min-height: 100vh;
  padding: 28px 20px 48px;
  background: #151c27;
  color: #edf2fb;
  font: 16px/1.5 system-ui, sans-serif;
}
.sprite-preview > * { max-width: 560px; margin-left: auto; margin-right: auto; }
.back { display: block; color: #a4c5ff; margin-bottom: 32px; }
.eyebrow { color: #91b5ed; font-size: 12px; letter-spacing: 0.15em; }
h1 { font-size: 40px; line-height: 1.1; margin-top: 8px; margin-bottom: 12px; }
.intro { color: #b7c4d9; }
.stage { margin-top: 24px; border: 1px solid #38485f; background: #202c3c; border-radius: 12px; }
.stage svg { display: block; width: 100%; height: auto; }
.controls, .poses { display: flex; flex-wrap: wrap; gap: 10px; margin-top: 20px; align-items: center; }
button {
  min-height: 44px;
  padding: 10px 16px;
  border: 1px solid #52637c;
  border-radius: 6px;
  color: #edf2fb;
  background: #27364c;
  font: inherit;
  cursor: pointer;
}
button[aria-pressed='true'] { border-color: #9ec4ff; box-shadow: inset 0 0 0 1px #9ec4ff; }
button.attack { background: #2559a2; }
button:focus-visible, a:focus-visible { outline: 3px solid #b8d3ff; outline-offset: 3px; }
label { display: inline-flex; gap: 8px; align-items: center; min-height: 44px; }
input { width: 18px; height: 18px; }
.phase { color: #b7c4d9; }
.size-check { margin-top: 32px; border-top: 1px solid #38485f; }
.walk-review { margin-top: 32px; border-top: 1px solid #38485f; }
.size-check h2 { font-size: 16px; font-weight: 500; }
.walk-review h2 { font-size: 16px; font-weight: 500; }
.size-check svg { display: block; margin: auto; }
</style>
