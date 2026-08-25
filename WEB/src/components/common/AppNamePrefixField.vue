<template>
  <!-- The title a person is addressed by, in a box of its own to the LEFT of their first name.
       Free text with the common titles as suggestions, exactly like the client name's generational
       suffix: the list is what most people need, not all any person may have, and a title nobody thought
       to seed is not a reason to address somebody wrongly. -->
  <app-text-field
    v-model="model"
    :label="label"
    :placeholder="placeholder"
    :readonly="readonly"
    :disable="disable"
    :error="tooLong"
    :error-message="`A ${label.toLowerCase()} is at most ${MAX_LENGTH} characters.`"
  >
    <template #append>
      <q-icon v-if="readonly" name="o_lock" size="18px" color="grey-6" />
      <q-btn
        v-else-if="!disable" flat dense round size="sm" icon="o_arrow_drop_down" color="grey-7"
        :aria-label="`${label} suggestions`"
      >
        <q-menu anchor="bottom end" self="top end" auto-close>
          <q-list dense style="min-width: 170px;">
            <q-item
              v-for="opt in NAME_PREFIXES" :key="opt.value"
              clickable :active="model === opt.value" active-class="bg-grey-2 text-primary"
              @click="model = opt.value"
            >
              <q-item-section>
                <q-item-label>{{ opt.label }}</q-item-label>
                <q-item-label caption>{{ opt.caption }}</q-item-label>
              </q-item-section>
            </q-item>
            <q-separator />
            <q-item clickable :disable="!model" @click="model = ''">
              <q-item-section class="text-grey-7">No {{ label.toLowerCase() }}</q-item-section>
            </q-item>
          </q-list>
        </q-menu>
      </q-btn>
    </template>
  </app-text-field>
</template>

<script setup>
// The name PREFIX field — Mr., Mrs., Ms., Dr. — asked beside every First Name in the app.
//
// It is a field of its own rather than something typed into the name because a person is FILED under a
// given name and a family name, and a title is neither: "Dr. Jane" in a first-name box is a person
// nobody finds by searching for their name, and it is the same mistake the client name's suffix box
// exists to prevent at the other end of the name.
//
// One component for every screen that asks it — the public intake form and its contacts, the Person
// record, a user's account, somebody's own profile — so the suggestions, the cap and the way it clears
// are the same wherever it is asked.
import { computed } from "vue";
import AppTextField from "components/common/AppTextField.vue";

const model = defineModel({ type: String, default: "" });

defineProps({
  // "Prefix" everywhere today. A prop because the box is small and the word above it is all the
  // explanation it gets, so a screen that calls it something else can.
  label: { type: String, default: "Prefix" },
  placeholder: { type: String, default: "Mr." },
  readonly: { type: Boolean, default: false },
  disable: { type: Boolean, default: false }
});

// Mirrors Person.Prefix / the REMS payload's prefix columns, both nvarchar(16). Checked here so the box
// says so while it is being typed rather than the save coming back with a 400 on a courtesy title.
const MAX_LENGTH = 16;
const tooLong = computed(() => (model.value?.trim().length || 0) > MAX_LENGTH);

// The suggestions. Deliberately short: these are the titles nearly every record needs, and the field
// stays free text for the ones it does not cover — Rev., Hon., a rank, a title in another language.
const NAME_PREFIXES = [
  { value: "Mr.", label: "Mr.", caption: "A man" },
  { value: "Mrs.", label: "Mrs.", caption: "A married woman" },
  { value: "Ms.", label: "Ms.", caption: "A woman, marital status not stated" },
  { value: "Miss", label: "Miss", caption: "An unmarried woman" },
  { value: "Mx.", label: "Mx.", caption: "Gender-neutral" },
  { value: "Dr.", label: "Dr.", caption: "A doctorate, or a physician" },
  { value: "Prof.", label: "Prof.", caption: "A professor" }
];
</script>
