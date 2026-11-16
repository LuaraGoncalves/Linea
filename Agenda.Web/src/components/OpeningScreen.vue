<script setup>
import { NotebookPen } from '@lucide/vue'

defineProps({
  loading: { type: Boolean, default: true },
  status: { type: String, default: 'Abrindo sua agenda...' },
  error: { type: String, default: '' }
})
</script>

<template>
  <section class="opening-screen" aria-labelledby="opening-title">
    <img v-if="loading" class="opening-notebook" src="/linea-notebook.svg" width="200" height="160" alt="" aria-hidden="true" />
    <NotebookPen v-else class="opening-symbol" :size="64" aria-hidden="true" />
    <h1 id="opening-title">Linea</h1>
    <p class="opening-tagline">coloque suas anotações em dia</p>
    <p v-if="loading" class="opening-status" role="status" aria-live="polite">{{ status }}</p>
    <div v-else class="opening-recovery">
      <p class="message" role="alert">{{ error }}</p>
      <slot name="recovery" />
    </div>
    <div class="opening-actions"><slot /></div>
  </section>
</template>
