<template>
  <q-icon name="o_help_outline" size="20px" color="grey-6" class="cursor-pointer app-help-hint">
    <q-tooltip>Guidance &amp; examples</q-tooltip>
    <q-menu anchor="bottom right" self="top right" max-width="380px">
      <div class="q-pa-md">
        <div class="text-subtitle2 q-mb-xs">{{ title }}</div>
        <div v-if="description" class="text-body2 text-grey-8 q-mb-sm app-help-desc">{{ description }}</div>
        <q-markup-table v-if="examples.length" flat dense bordered separator="cell" class="app-help-table">
          <thead>
            <tr>
              <th class="text-left">Example</th>
              <th class="text-left">Meaning</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(ex, i) in examples" :key="i">
              <td class="text-left"><code>{{ ex.code }}</code></td>
              <td class="text-left">{{ ex.desc }}</td>
            </tr>
          </tbody>
        </q-markup-table>
      </div>
    </q-menu>
  </q-icon>
</template>

<script setup>
// A small "?" affordance that reveals field guidance and a worked-example table on click.
defineProps({
  title: { type: String, default: "Help" },
  description: { type: String, default: "" },
  // Array of { code, desc } rows rendered as an examples chart.
  examples: { type: Array, default: () => [] }
});
</script>

<style scoped>
.app-help-hint:hover {
  color: var(--q-primary);
}
.app-help-desc {
  white-space: pre-line;
}
.app-help-table code {
  background: #f1f3f5;
  padding: 1px 4px;
  border-radius: 4px;
  font-size: 12px;
}
</style>
