export default {
	// Accept the Conventional Commits `!` breaking-change marker (`build!: ...`, `feat(api)!: ...`). Without a
	// parser preset commitlint's default header pattern has no `!`, so such a header parses as having no type and
	// no subject and fails type-empty/subject-empty.
	parserPreset: {
		parserOpts: {
			headerPattern: /^(\w*)(?:\((.*)\))?!?: (.*)$/,
			breakingHeaderPattern: /^(\w*)(?:\((.*)\))?!: (.*)$/,
			headerCorrespondence: ['type', 'scope', 'subject'],
		},
	},
	rules: {
		'body-leading-blank': [1, 'always'],
		'body-max-line-length': [2, 'always', 100],
		'footer-leading-blank': [1, 'always'],
		'footer-max-line-length': [2, 'always', 100],
		'header-max-length': [2, 'always', 100],
		'header-trim': [2, 'always'],
		'subject-case': [2, 'never', ['sentence-case', 'start-case', 'pascal-case', 'upper-case']],
		'subject-empty': [2, 'never'],
		'subject-full-stop': [2, 'never', '.'],
		'type-case': [2, 'always', 'lower-case'],
		'type-empty': [2, 'never'],
		'type-enum': [
			2,
			'always',
			['build', 'chore', 'ci', 'docs', 'feat', 'fix', 'perf', 'refactor', 'revert', 'style', 'test'],
		],
	},
};
