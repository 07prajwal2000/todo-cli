import { defineConfig } from 'vitepress'

export default defineConfig({
  title: 'todo',
  description:
    'A single-binary .NET 10 CLI TODO manager backed by a Google Sheet, with a local dashboard.',

  base: '/todo-cli/',

  cleanUrls: false,
  lastUpdated: true,
  metaChunk: true,

  markdown: {
    lineNumbers: true,
  },

  head: [
    ['meta', { name: 'theme-color', content: '#5e81ac' }],
    ['meta', { property: 'og:type', content: 'website' }],
    ['meta', { property: 'og:title', content: 'todo — CLI TODO manager on Google Sheets' }],
  ],

  themeConfig: {
    nav: [
      { text: 'Guide', link: '/guide/getting-started', activeMatch: '^/guide/' },
      { text: 'Reference', link: '/reference/sheet-schema', activeMatch: '^/reference/' },
      { text: 'Troubleshooting', link: '/guide/troubleshooting' },
    ],

    sidebar: [
      {
        text: 'Getting started',
        items: [
          { text: 'Install and first task', link: '/guide/getting-started' },
          { text: 'Google Cloud setup', link: '/guide/google-cloud-setup' },
        ],
      },
      {
        text: 'Using todo',
        items: [
          { text: 'Commands', link: '/guide/commands' },
          { text: 'Spaces', link: '/guide/spaces' },
          { text: 'Dashboard', link: '/guide/dashboard' },
          { text: 'Configuration', link: '/guide/configuration' },
        ],
      },
      {
        text: 'Reference',
        items: [
          { text: 'Sheet schema', link: '/reference/sheet-schema' },
          { text: 'Architecture', link: '/reference/architecture' },
          { text: 'Support matrix', link: '/reference/support-matrix' },
        ],
      },
      {
        text: 'Help',
        items: [{ text: 'Troubleshooting', link: '/guide/troubleshooting' }],
      },
    ],

    outline: {
      level: [2, 3],
      label: 'On this page',
    },

    search: {
      provider: 'local',
      options: {
        detailedView: true,
      },
    },

    socialLinks: [],

    footer: {
      message: 'Backed by the Google Sheets v4 API. No database, no sync service.',
      copyright: 'todo CLI',
    },

    docFooter: {
      prev: 'Previous page',
      next: 'Next page',
    },

    sidebarMenuLabel: 'Menu',
    returnToTopLabel: 'Back to top',
    darkModeSwitchLabel: 'Appearance',
    lightModeSwitchTitle: 'Switch to light theme',
    darkModeSwitchTitle: 'Switch to dark theme',
  },
})
