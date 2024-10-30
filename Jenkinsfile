@Library('standard-library') _
pipeline {
    agent {
        label 'imagechecker'
    }
    environment {
        REGISTRY='registry.guildswarm.org'
        //TBD - Change this to the environment name with branch name in the future
        ENVIRONMENT = "${env.BRANCH_NAME == 'integration' ? 'staging' : (env.BRANCH_NAME == 'main' || env.BRANCH_NAME == 'master') ? 'production' : env.BRANCH_NAME}"
        IMAGE='the_good_framework'
    }
    stages {
        stage('Build Docker Images') {
            steps {
                script {
                    def version = readFile('version').trim()
                    env.VERSION = version
                    sh ''' 
                        find . \\( -name "*.csproj" -o -name "*.sln" -o -name "NuGet.docker.config" \\) -print0 \
                        | tar -cvf projectfiles.tar -T -
                    '''
                    try {
                        withCredentials([[$class: 'UsernamePasswordMultiBinding', credentialsId: "harbor-base-images", usernameVariable: 'DOCKER_USERNAME', passwordVariable: 'DOCKER_PASSWORD']]) {
                            sh "docker login -u \'${DOCKER_USERNAME}' -p \'${DOCKER_PASSWORD}' ${REGISTRY}"
                            sh "docker build . --build-arg ENVIRONMENT=\"${ENVIRONMENT}\" -t ${REGISTRY}/${ENVIRONMENT}/${IMAGE}:${version} -t ${REGISTRY}/${ENVIRONMENT}/${IMAGE}:latest"
                            sh 'docker logout'
                        }
                    } finally {
                        sh "rm -f projectfiles.tar"
                    }
                }
            }
        }
        stage('Test Vulnerabilities') {
            steps {
                script {
                    if (env.CHANGE_ID == null) {
                        // Trivy can't download the database from the internet, so we need to provide it. 
                        // Solution: https://github.com/aquasecurity/trivy/discussions/7668
                        sh "trivy image --db-repository public.ecr.aws/aquasecurity/trivy-db \
                                --java-db-repository public.ecr.aws/aquasecurity/trivy-java-db \
                                --exit-code 1 --quiet ${REGISTRY}/${ENVIRONMENT}/${IMAGE}:latest"
                    } else {
                        echo "Avoiding Scan in PR"
                    }
                }
            }
        }
        stage('Push Docker Images') {
            steps {
                script {
                    if (env.CHANGE_ID == null) {
                        withCredentials([[$class: 'UsernamePasswordMultiBinding', credentialsId: "harbor-${ENVIRONMENT}", usernameVariable: 'DOCKER_USERNAME', passwordVariable: 'DOCKER_PASSWORD']]) {
                            sh "docker login -u \'${DOCKER_USERNAME}' -p \'${DOCKER_PASSWORD}' ${REGISTRY}"
                            sh "docker push ${REGISTRY}/${ENVIRONMENT}/${IMAGE}:${version}"
                            sh "docker push ${REGISTRY}/${ENVIRONMENT}/${IMAGE}:latest"
                            sh 'docker logout'
                        }
                    } else {
                        echo "Avoiding push for PR"
                    }
                }
            }
        }
        stage('Remove Docker Images') {
            steps {
                script {
                    sh "docker rmi ${REGISTRY}/${ENVIRONMENT}/${IMAGE}:$version"
                    sh "docker rmi ${REGISTRY}/${ENVIRONMENT}/${IMAGE}:latest"
                }
            }
        }
    }
    post {
        always {
            sh 'rm -rf *'
        }
        success {
            build job: "backend/GSWB.Common/${ENVIRONMENT}", wait: false
        }
        failure {
            script{
                pga.slack_webhook("backend")
            }
        }
    }
}