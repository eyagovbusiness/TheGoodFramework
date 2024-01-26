@Library('standard-library@latest') _
pipeline {
    agent {
        label 'imagechecker'
    }
    environment {
        REGISTRY='registry.guildswarm.org'
        IMAGE='the_good_framework'
        REPO='staging'
    }
    stages{
        stage('Build Docker Images') {
            steps {
                script {
                    container ('dockertainer'){
                          def version = readFile('version').trim()
                          env.VERSION = version
                          sh''' find . \\( -name "*.csproj" -o -name "*.sln" -o -name "NuGet.docker.config" \\) -print0 \
                           | tar -cvf projectfiles.tar -T -
                           '''
						  try {
						    withCredentials([[$class: 'UsernamePasswordMultiBinding', credentialsId: 'harbor-base-images', usernameVariable: 'DOCKER_USERNAME', passwordVariable: 'DOCKER_PASSWORD']]) {
                                sh "docker login -u \'${DOCKER_USERNAME}' -p \'$DOCKER_PASSWORD' $REGISTRY"
							    sh "docker build . --build-arg ENVIRONMENT='baseimages' -t ${REGISTRY}/${REPO}/${IMAGE}:${version} -t ${REGISTRY}/${REPO}/${IMAGE}:latest"
							    sh 'docker logout'
						    }
						  } finally {
							    sh "rm -f projectfiles.tar"
							  }
                        }
                    }
                }
            }
        stage('Test Vulnerabilities'){
            steps{
                container('dockertainer'){
                    sh "trivy image --quiet ${REGISTRY}/${REPO}/${IMAGE}:latest"
                }
            }
        }
        stage('Push Docker Images') {
            steps {
                script {
                    container ('dockertainer'){
                                withCredentials([[$class: 'UsernamePasswordMultiBinding', credentialsId: 'harbor-staging', usernameVariable: 'DOCKER_USERNAME', passwordVariable: 'DOCKER_PASSWORD']]) {
                                    sh "docker login -u \'${DOCKER_USERNAME}' -p $DOCKER_PASSWORD $REGISTRY"
                                    sh "docker push ${REGISTRY}/${REPO}/${IMAGE}:$version"
                                    sh "docker push ${REGISTRY}/${REPO}/${IMAGE}:latest"
                                    sh 'docker logout'
                                }
                            }
                        }
                    }
                }
        stage('Remove Docker Images') {
            steps {
                script {
                    container ('dockertainer'){
                            sh "docker rmi ${REGISTRY}/${REPO}/${IMAGE}:$version"
                            sh "docker rmi ${REGISTRY}/${REPO}/${IMAGE}:latest"
                            }
                        }
                    }
                }
            }  
    post {
        always{
            sh 'rm -rf *'
        }
        failure {
            pga.slack_webhook("backend")
        }
    }
}