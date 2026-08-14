import antfu from '@antfu/eslint-config'

export default antfu(
  {
    vue: true,
    typescript: true,
    stylistic: {
      indent: 2,
      quotes: 'single',
      semi: false,
    },
    // 对齐仓库现有约定（template 在前、单参数箭头带括号、单行 if 允许），
    // 宽松基线：先接入工具链，规则后续迭代收紧
    rules: {
      'no-console': 'off',
      'antfu/no-top-level-await': 'off',
      'antfu/if-newline': 'off',
      'vue/multi-word-component-names': 'off',
      'vue/block-order': ['error', { order: ['template', 'script', 'style'] }],
      'vue/singleline-html-element-content-newline': 'off',
      'test/no-import-node-test': 'off',
      'style/brace-style': ['error', '1tbs', { allowSingleLine: true }],
      'style/arrow-parens': ['error', 'always'],
      'style/max-statements-per-line': 'off',
      'jsonc/sort-keys': 'off',
      'perfectionist/sort-imports': 'off',
      'perfectionist/sort-exports': 'off',
      'perfectionist/sort-objects': 'off',
      'perfectionist/sort-vue-attributes': 'off',
      'perfectionist/sort-named-imports': 'off',
      'unicorn/filename-case': 'off',
      'regexp/no-super-linear-backtracking': 'off',
      'regexp/optimal-quantifier-concatenation': 'off',
    },
  },
  {
    // 构建配置文件允许使用 process 全局
    files: ['**/vite.config.ts', '**/uno.config.ts', 'eslint.config.mjs'],
    rules: {
      'node/prefer-global/process': 'off',
    },
  },
  {
    ignores: [
      '**/dist/**',
      '**/node_modules/**',
      'backend/**',
      'services/**',
      '**/bin/**',
      '**/obj/**',
      '**/.venv/**',
      'docs/**',
      '**/*.log',
      'dev.mjs',
      'pnpm-lock.yaml',
    ],
  },
)
